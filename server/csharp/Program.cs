using System.Numerics;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Qixr.Companion;

string root=Path.GetFullPath(args.Length>0?args[0]:".");
int syntaxFiles=0;
foreach(string path in Directory.GetFiles(Path.Combine(root,"Assets/Scripts/Companion"),"*.cs")
    .Concat(new[]{Path.Combine(root,"Assets/Scripts/Qubit/QubitManager.cs"),Path.Combine(root,"Assets/Scripts/Colocation/QuantumSessionState.cs"),Path.Combine(root,"Assets/Editor/QuantumCompanionSetup.cs")}))
{
    var tree=CSharpSyntaxTree.ParseText(File.ReadAllText(path));
    var errors=tree.GetDiagnostics().Where(d=>d.Severity==DiagnosticSeverity.Error).ToArray();
    if(errors.Length>0) throw new Exception(path+": "+string.Join("; ",errors.Select(e=>e.ToString())));
    syntaxFiles++;
}
QixrTwoQubitBaselineChecks.Run();
QixrThreeQubitEngineChecks.Run();
QixrPhase2MetricChecks.Run();
var options=new JsonSerializerOptions {IncludeFields=true,WriteIndented=true};
var results=new List<object>();
foreach(string name in new[]{"product","bell","ghz","w","classical","mixed"})
{
    var matrix=new ComplexMatrix(8,8);
    if(name=="mixed") { for(int i=0;i<8;i++) matrix[i,i]=.125; }
    else if(name=="classical") { matrix[0,0]=.5; matrix[7,7]=.5; }
    else
    {
        int[] occupied=name=="product"?new[]{0}:name=="bell"?new[]{0,6}:name=="ghz"?new[]{0,7}:new[]{1,2,4};
        foreach(int a in occupied) foreach(int b in occupied) matrix[a,b]=1.0/occupied.Length;
    }
    var engine=new QuantumStateEngine(3); engine.SetDensityMatrix(matrix);
    var metrics=EntanglementMetrics.Build(engine.DensityMatrix,3,engine.StateVersion);
    double[] wire=new double[128]; int offset=0;
    for(int r=0;r<8;r++) for(int c=0;c<8;c++) { wire[offset++]=matrix[r,c].Real; wire[offset++]=matrix[r,c].Imaginary; }
    var dto=new QuantumSnapshotDto {roomId="cross-language",authorityEpoch=1,snapshotId=name,stateVersion=engine.StateVersion,
        capturedAtUtc="2026-10-28T00:00:00Z",qubitCount=3,density=wire,couplings=Array.Empty<CouplingDto>(),presentation=new PresentationDto {scene="headless-reference"}};
    results.Add(new { snapshot=dto, nodes=metrics.Nodes.Select(n=>new {qubitId=n.QubitId,purity=n.Purity,radius=n.BlochRadius,renyi2Nats=n.Renyi2Entropy}),
        pairs=metrics.Pairs.Select(p=>new {first=p.FirstQubitId,second=p.SecondQubitId,logarithmicNegativityBits=p.LogarithmicNegativity,mutualInformationBits=p.MutualInformationBits}) });
}
string destination=Path.Combine(root,".runtime/csharp-goldens.json");
Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.WriteAllText(destination,JsonSerializer.Serialize(results,options));
float[] input={-.5f,0,.5f,1,-1}; int rate;
float[] output=QuantumWav.Decode(QuantumWav.Encode(input,16000),out rate);
if(rate!=16000 || output.Length!=input.Length || input.Zip(output,(a,b)=>Math.Abs(a-b)).Max()>1.0/32767) throw new Exception("WAV round-trip failed");
Console.WriteLine($"PASS: original engine/metric checks, PCM16 round-trip, {syntaxFiles} C# syntax trees; wrote {destination}");
