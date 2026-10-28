"""Local CPU speech workers; no user audio is written to disk."""
import asyncio
import io
from pathlib import Path
import threading
import wave

import numpy as np


class Voice:
    def __init__(self, model_dir, enabled=True):
        self.model_dir, self.enabled = Path(model_dir), enabled
        self.asr, self.tts, self.error = None, {}, None
        self.asr_lock, self.tts_lock = threading.Lock(), threading.Lock()

    def load(self):
        if not self.enabled: return
        try:
            import sherpa_onnx
            from faster_whisper import WhisperModel
            self.asr = WhisperModel(str(self.model_dir/'whisper-small'), device='cpu', compute_type='int8', cpu_threads=6, num_workers=1)
            for lang, dirname in [('ko', 'vits-mimic3-ko_KO-kss_low'), ('en', 'vits-piper-en_US-lessac-medium-int8')]:
                path = self.model_dir/dirname
                config = sherpa_onnx.OfflineTtsConfig(model=sherpa_onnx.OfflineTtsModelConfig(
                    vits=sherpa_onnx.OfflineTtsVitsModelConfig(model=str(next(path.glob('*.onnx'))),
                        tokens=str(path/'tokens.txt'), data_dir=str(path/'espeak-ng-data')),
                    num_threads=2, provider='cpu', debug=False), max_num_sentences=1)
                if not config.validate(): raise ValueError('Invalid TTS configuration: '+lang)
                self.tts[lang] = sherpa_onnx.OfflineTts(config)
        except Exception as e:
            self.error = type(e).__name__+': '+str(e)[:200]

    def transcribe_sync(self, wav, language):
        if self.asr is None: raise RuntimeError('ASR unavailable')
        with wave.open(io.BytesIO(wav)) as f:
            if f.getnchannels() != 1 or f.getsampwidth() != 2 or f.getframerate() != 16000 or f.getnframes() > 16000*15:
                raise ValueError('Expected at most 15 seconds of mono PCM16 WAV at 16 kHz')
            samples = np.frombuffer(f.readframes(f.getnframes()), dtype='<i2').astype(np.float32)/32768
        with self.asr_lock:
            glossary = ('큐빗, 양자, 하다마드, 파울리, 블로흐 구, 얽힘, 중첩, 측정, 확률, 상태, 리본, 게이트, 위상, 순도.'
                        if language=='ko' else 'QIXR, qubit, quantum, Hadamard, Pauli, Bloch sphere, entanglement, probability, measurement, phase, purity.')
            segments, _ = self.asr.transcribe(samples, language=language, beam_size=5, temperature=0,
                condition_on_previous_text=False, vad_filter=True, initial_prompt=glossary)
            return ' '.join(s.text.strip() for s in segments).strip()

    def speak_sync(self, text, language):
        if language not in self.tts: raise RuntimeError('TTS unavailable')
        # Korean phonemizer handles native words more reliably than Latin gate labels.
        if language == 'ko':
            for a,b in [('Q1','큐빗 일'),('Q2','큐빗 이'),('Q3','큐빗 삼'),('measureZ','지 기저 측정'),('H·X·Z·S','하다마드, 파울리 엑스, 파울리 지, 위상 에스')]:
                text = text.replace(a,b)
        with self.tts_lock:
            audio = self.tts[language].generate(text, sid=0, speed=1.12)
        data = np.clip(audio.samples, -1, 1)
        out = io.BytesIO()
        with wave.open(out, 'wb') as f:
            f.setnchannels(1); f.setsampwidth(2); f.setframerate(audio.sample_rate)
            f.writeframes((data*32767).astype('<i2').tobytes())
        return out.getvalue()

    async def run(self, function, *args):
        task = asyncio.create_task(asyncio.to_thread(function, *args))
        try:
            return await asyncio.shield(task)
        except asyncio.CancelledError:
            # Let the native call finish under its lock; never reuse an active engine.
            await task
            raise
