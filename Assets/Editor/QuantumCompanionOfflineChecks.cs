using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Qixr.Companion;
using UnityEditor;
using UnityEngine;

public static class QuantumCompanionOfflineChecks
{
    [Serializable] private class Fixture { public bool fixtureOnly; public int protocolVersion; public QuantumSnapshotDto[] snapshots; public Case[] cases; }
    [Serializable] private class Case { public string name; public CompanionQuestion request; public CompanionJob job; }
    private static int assertions;
    [MenuItem("QIXR/Companion/Run offline checks")]
    public static void Run()
    {
        QuantumCompanionClient.SuppressBootstrap=true;
        assertions=0;
        var fixture=JsonUtility.FromJson<Fixture>(File.ReadAllText("docs/handoff/fixtures/protocol-v1.json"));
        Check(fixture.fixtureOnly && fixture.protocolVersion==1,"Synthetic protocol-v1 fixture");
        foreach(var snapshot in fixture.snapshots)
        {
            var roundTrip=JsonUtility.FromJson<QuantumSnapshotDto>(JsonUtility.ToJson(snapshot));
            Check(roundTrip.density.Length==128 && roundTrip.basisOrder=="q0-msb","Density wire layout");
            for(int i=0;i<128;i++) Check(roundTrip.density[i]==snapshot.density[i],"Density precision");
        }
        foreach(var example in fixture.cases)
        {
            CompanionJob job=JsonUtility.FromJson<CompanionJob>(JsonUtility.ToJson(example.job));
            var owner=new CompanionTurn { Conversation="mock-conversation",Request=example.request.requestId,
                Selection=example.request.selectionId,Epoch=example.job.result.authorityEpoch,LocalGeneration=7,ServerGeneration=job.generation };
            Check(owner.Matches(job,"mock-conversation",owner.Selection,7,owner.Epoch),example.name+" accepted");
            Check(!owner.Matches(job,"mock-conversation","different-selection",7,owner.Epoch),"Stale selection rejected");
            Check(!owner.Matches(job,"mock-conversation",owner.Selection,8,owner.Epoch),"Cancelled generation rejected");
            Check(!owner.Matches(job,"different-conversation",owner.Selection,7,owner.Epoch),"Old conversation rejected");
            Check(!owner.Matches(job,"mock-conversation",owner.Selection,7,owner.Epoch+1),"Old authority epoch rejected");
            if(job.result.status=="quiet") Check(string.IsNullOrEmpty(job.result.text) && job.audioCount==0,"Quiet has no speech");
        }
        var config=new CompanionConfig { serverUrl="https://lab.invalid:8786",certificateSha256=new string('a',64),roomId="offline-test",joinToken="synthetic-test-only",language="en",simulationMode="fusion" };
        Check(QuantumCompanionConfiguration.Validate(config,out _),"Valid offline config shape");
        config.serverUrl="http://lab.invalid"; Check(!QuantumCompanionConfiguration.Validate(config,out _),"HTTP rejected");
        config.serverUrl="https://localhost:8786"; Check(!QuantumCompanionConfiguration.Validate(config,out _),"Headset localhost rejected");
        config.serverUrl="https://lab.invalid:8786"; config.certificateSha256="";
        Check(!QuantumCompanionConfiguration.Validate(config,out _),"Missing certificate pin rejected");
        config.certificateSha256=new string('a',64); config.authenticationMode="password"; config.joinToken=null;
        Check(QuantumCompanionConfiguration.Validate(config,out _),"Public password profile needs no join token");
        config.joinToken="synthetic-test-only";
        Check(!QuantumCompanionConfiguration.Validate(config,out _),"Password profiles reject embedded join tokens");
        config.joinToken=null;
        string publicProfile=JsonUtility.ToJson(config);
        Check(!publicProfile.Contains("synthetic-test-only"),"Public profile contains no access password/token");
        Check(QuantumCompanionConfiguration.TryParse(publicProfile,out var passwordConfig,out _) && passwordConfig.authenticationMode=="password","Password profile round trip");
        var engine=new QuantumStateEngine(3);
        ComplexMatrix source=engine.DensityMatrix.Clone();
        var sandbox=new QuantumStateEngine(3); sandbox.SetDensityMatrix(source.Clone()); sandbox.ApplySingleQubitGate(0,Gates.Hadamard());
        Check(engine.DensityMatrix[0,0]==source[0,0] && engine.DensityMatrix[0,1]==source[0,1],"Preview leaves live source unchanged");
        float[] pcm={-.5f,0,.5f};
        float[] decoded=QuantumWav.Decode(QuantumWav.Encode(pcm,22050),out int rate);
        Check(rate==22050 && decoded.Length==pcm.Length && Math.Abs(decoded[0]-pcm[0])<0.0001,"TTS WAV header rate preserved");
        CheckMockTransport();
        CheckJournal();
        var wideVersion=JsonUtility.FromJson<QuantumSnapshotDto>("{\"stateVersion\":9007199254740993}");
        Check(JsonUtility.FromJson<QuantumSnapshotDto>(JsonUtility.ToJson(wideVersion)).stateVersion==9007199254740993L,"64-bit version round-trip");
        QixrTwoQubitBaselineChecks.Run(); QixrThreeQubitEngineChecks.Run(); QixrPhase2MetricChecks.Run();
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/companion-offline-checks.json", "{\"assertions\":"+assertions+",\"passed\":true,\"liveNetwork\":false}");
        Debug.Log("QIXR OFFLINE CHECKS PASSED: "+assertions+" assertions; no gateway, model or speech worker started.");
    }
    private static void CheckMockTransport()
    {
        var go=new GameObject("Offline companion transport test"); go.SetActive(false);
        var client=go.AddComponent<QuantumCompanionClient>();
        try
        {
            var mock=new MockTransport(); client.Transport=mock;
            // Execute the actual client send coroutine with injected I/O. No Start/Play Mode/bootstrap.
            var send=typeof(QuantumCompanionClient).GetMethod("SendBytes",BindingFlags.NonPublic|BindingFlags.Instance);
            byte[] received=null;
            Drain((IEnumerator)send.Invoke(client,new object[]{"GET","/fixture",null,null,"mock-token",new Action<byte[]>(x=>received=x)}));
            Check(mock.Calls==1 && received!=null,"Client uses injected transport");
            mock.Code=422;
            Drain((IEnumerator)send.Invoke(client,new object[]{"GET","/fixture",null,null,"mock-token",null}));
            Check(mock.Calls==2,"Invalid payload is not retried");
            mock.Code=429;
            Drain((IEnumerator)send.Invoke(client,new object[]{"GET","/fixture",null,null,"mock-token",null}));
            Check(mock.Calls==5,"Busy responses have a three-attempt retry bound");
            var conversation=typeof(QuantumCompanionClient).GetField("conversationId",BindingFlags.NonPublic|BindingFlags.Instance);
            conversation.SetValue(client,"mock-conversation"); mock.Code=404;
            Drain((IEnumerator)send.Invoke(client,new object[]{"GET","/v1/conversations/mock-conversation/requests/missing",null,null,"mock-token",null}));
            Check((string)conversation.GetValue(client)=="mock-conversation","Missing job does not create a rejoin loop");
            conversation.SetValue(client,null);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
    private static void Drain(IEnumerator routine)
    {
        int steps=0;
        while(routine.MoveNext())
        {
            if(++steps>100) throw new Exception("Mock coroutine did not finish");
            // EditMode advances mock I/O immediately; wall-clock backoff is a PlayMode concern.
            if(routine.Current is IEnumerator nested && !(routine.Current is CustomYieldInstruction)) Drain(nested);
        }
    }
    private static void CheckJournal()
    {
        var flags=BindingFlags.Static|BindingFlags.NonPublic;
        var engineField=typeof(QubitManager).GetField("engine",flags);
        var couplingField=typeof(QubitManager).GetField("pairCouplings",flags);
        object previousEngine=engineField.GetValue(null), previousCouplings=couplingField.GetValue(null);
        bool publishing=QuantumContextExporter.AllowStandalonePublishing, participant=QuantumContextExporter.JoinOnlyParticipant;
        var go=new GameObject("Offline event journal"); go.SetActive(false);
        try
        {
            var engine=new QuantumStateEngine(3);
            engineField.SetValue(null,engine); couplingField.SetValue(null,new double[3,3]);
            QuantumContextExporter.AllowStandalonePublishing=true; QuantumContextExporter.JoinOnlyParticipant=false;
            var journal=go.AddComponent<QuantumEventJournal>();
            var exporter=go.GetComponent<QuantumContextExporter>(); exporter.AuthorityEpoch=1; exporter.RoomId="fixture-room";
            typeof(QuantumEventJournal).GetField("exporter",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(journal,exporter);
            var before=engine.DensityMatrix.Clone(); long version=engine.StateVersion;
            engine.ApplySingleQubitGate(0,Gates.Hadamard());
            var mutation=new QuantumMutation { Actor="offline",Operation="H",Target=0,Before=before,
                After=engine.DensityMatrix.Clone(),BeforeVersion=version,AfterVersion=engine.StateVersion };
            typeof(QuantumEventJournal).GetMethod("Record",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(journal,new object[]{mutation});
            var entry=journal.Peek();
            Check(entry!=null && entry.Before.eventSequence==0 && entry.After.eventSequence==1 && entry.Event.sequence==1,"Journal before/after/event sequence");
            Check(entry.Event.beforeSnapshotId==entry.Before.snapshotId && entry.Event.afterSnapshotId==entry.After.snapshotId,"Journal references actual immutable snapshots");
            Check(ReferenceEquals(entry,journal.Peek()) && journal.Count==1,"Failed upload retains original IDs");
            journal.Acknowledge(); Check(journal.Count==0,"Only acknowledgement removes entry");
            journal.ClearJournal(); Check(exporter.EventSequence==0 && !journal.Overflowed,"Epoch boundary clears journal");
            for(int i=0;i<129;i++) typeof(QuantumEventJournal).GetMethod("Record",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(journal,new object[]{mutation});
            Check(journal.Count==128 && journal.Overflowed,"Journal overflow is bounded and establishes a new history boundary");
            journal.ClearJournal(); Check(journal.Count==0 && !journal.Overflowed,"Old epoch entries cannot survive reset");
        }
        finally
        {
            engineField.SetValue(null,previousEngine); couplingField.SetValue(null,previousCouplings);
            QuantumContextExporter.AllowStandalonePublishing=publishing; QuantumContextExporter.JoinOnlyParticipant=participant;
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
    private sealed class MockTransport : IQuantumCompanionTransport
    {
        public int Calls; public long Code=200;
        public IEnumerator Send(string url,string method,byte[] body,string type,string token,string pin,Action<CompanionHttpResult> done)
        { Calls++; done(new CompanionHttpResult { Status=Code,Data=new byte[]{1,2,3} }); yield break; }
        public void AbortAll() { }
    }
    private static void Check(bool condition,string message)
    { assertions++; if(!condition) throw new Exception("Offline check failed: "+message); }
}
