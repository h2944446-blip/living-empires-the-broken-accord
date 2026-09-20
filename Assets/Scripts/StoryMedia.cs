using System;
using System.Collections.Generic;
using UnityEngine;

namespace LivingEmpires
{
    [Serializable] public class StoryLine { public string speaker, speaker_id, text; public float start,end; }
    [Serializable] public class StoryEvent { public string id,speaker,speaker_id,title,text,audio,image,context; public float audio_duration; public StoryLine[] lines; }
    [Serializable] public class StoryCollection { public StoryEvent[] events; }
    public sealed class StoryMedia : MonoBehaviour
    {
        public AudioSource Voice, Effects, Ambience;
        public float VoiceVolume=1, EffectsVolume=.55f, AmbienceVolume=.2f;
        public StoryEvent Current;
        readonly Dictionary<string,StoryEvent> events = new Dictionary<string,StoryEvent>();
        readonly Dictionary<string,AudioClip> sounds = new Dictionary<string,AudioClip>();
        readonly Dictionary<string,AudioClip> recordings = new Dictionary<string,AudioClip>();
        public IEnumerable<StoryEvent> All => events.Values;
        public bool HasRecordings { get { foreach(var entry in events.Values)if(HasRecording(entry.id))return true;return false; } }
        void Awake()
        {
            Voice=gameObject.AddComponent<AudioSource>(); Effects=gameObject.AddComponent<AudioSource>(); Ambience=gameObject.AddComponent<AudioSource>();
            Voice.playOnAwake=Effects.playOnAwake=Ambience.playOnAwake=false;
            VoiceVolume=PlayerPrefs.GetFloat("Voice",1); EffectsVolume=PlayerPrefs.GetFloat("Effects",.55f); AmbienceVolume=PlayerPrefs.GetFloat("Ambience",.2f);
            var data=Resources.Load<TextAsset>("Narrative/story");
            if(data!=null)foreach(var entry in JsonUtility.FromJson<StoryCollection>(data.text).events)events[entry.id]=entry;
            sounds["click"]=Tone("Ledger click",880,.065f); sounds["build"]=Tone("Wooden hammer",180,.24f);
            sounds["trade"]=Tone("Trade bell",659,.65f); sounds["error"]=Tone("Quiet error",120,.17f);
            Ambience.clip=River(); Ambience.loop=true; Ambience.Play(); ApplyVolumes();
        }
        public StoryEvent Get(string id) { StoryEvent e; return events.TryGetValue(id,out e)?e:null; }
        public AudioClip Recording(string id)
        {
            var entry=Get(id);if(entry==null||string.IsNullOrEmpty(entry.audio))return null;
            if(!recordings.TryGetValue(id,out var clip)){clip=Resources.Load<AudioClip>(entry.audio);recordings[id]=clip;}
            return clip;
        }
        public bool HasRecording(string id)=>Recording(id)!=null;
        public void Play(string id)
        {
            Stop(); Current=Get(id);Voice.clip=Recording(id);if(Voice.clip!=null)Voice.Play();
        }
        public void Stop(){ Voice.Stop();Voice.clip=null;Current=null; }
        public void Sound(string id){AudioClip clip;if(sounds.TryGetValue(id,out clip))Effects.PlayOneShot(clip);}
        public StoryLine Caption()
        {
            if(Current==null||!Voice.isPlaying||Current.lines==null)return null;
            foreach(var line in Current.lines)if(Voice.time>=line.start&&Voice.time<line.end)return line; return null;
        }
        public void ApplyVolumes(){Voice.volume=VoiceVolume;Effects.volume=EffectsVolume;Ambience.volume=AmbienceVolume;PlayerPrefs.SetFloat("Voice",VoiceVolume);PlayerPrefs.SetFloat("Effects",EffectsVolume);PlayerPrefs.SetFloat("Ambience",AmbienceVolume);}
        static AudioClip Tone(string name,float frequency,float seconds)
        {
            const int rate=24000; var samples=new float[(int)(rate*seconds)];
            for(int i=0;i<samples.Length;i++){float t=(float)i/rate; samples[i]=Mathf.Sin(t*frequency*Mathf.PI*2)*Mathf.Exp(-t*8)*.16f+Mathf.Sin(t*frequency*1.501f*Mathf.PI*2)*Mathf.Exp(-t*12)*.06f;}
            var c=AudioClip.Create(name,samples.Length,1,rate,false);c.SetData(samples,0);return c;
        }
        static AudioClip River()
        {
            const int rate=24000; var samples=new float[rate*8];var random=new System.Random(482);float low=0;
            for(int i=0;i<samples.Length;i++){float n=(float)random.NextDouble()*2-1;low=low*.96f+n*.04f;float envelope=Mathf.Sin(Mathf.PI*i/(samples.Length-1));samples[i]=low*.32f*envelope;}
            var c=AudioClip.Create("Original river ambience",samples.Length,1,rate,false);c.SetData(samples,0);return c;
        }
    }
}
