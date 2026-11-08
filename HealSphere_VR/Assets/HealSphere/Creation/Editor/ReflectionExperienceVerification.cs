using System;
using System.IO;
using System.Linq;
using System.Text;
using HealSphere.Breathing;
using HealSphere.Tripo;
using HealSphere.Wellness;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HealSphere.Creation.Editor
{
    /// <summary>Explicit zero-provider-cost test. Uses mock audio/WAV uploads; never starts a microphone or creates UI.</summary>
    [InitializeOnLoad]
    public static class ReflectionExperienceVerification
    {
        private const string Marker="Library/HealSphereReflectionPlaytest.request", Report="Library/HealSphereReflectionPlaytest.txt", Running="HealSphere.ReflectionVerification";
        private static ReflectionExperienceDirector director;
        private static CreationSessionDirector creation;
        private static CreationSessionPresenter presenter;
        private static TripoModelLoader backend;
        private static RabbitVoiceNarrator voice;
        private static ReflectionMicrophoneInput microphone;
        private static ParticleModelReveal reveal;
        private static int step, frames, inhale, exhale;
        private static string lastDiagnostic;
        private static bool checking, heard, surface;
        private static double deadline;
        private static float volume;
        private static string errors;
        static ReflectionExperienceVerification()
        {
            EditorApplication.update+=Tick;
            EditorApplication.playModeStateChanged+=state=>{
                if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Running,false))Begin();
                if(state==PlayModeStateChange.EnteredEditMode)SessionState.SetBool(Running,false);
            };
        }
        private static T One<T>()where T:Object=>Object.FindObjectsByType<T>(FindObjectsInactive.Include).Single();
        private static Button Button(string name)=>Object.FindObjectsByType<Button>(FindObjectsInactive.Include).Single(x=>x.name==name);
        private static TMP_Text Message=>Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include).Single(x=>x.name=="MomentMessage");
        [MenuItem("HealSphere/Reflection/Verify Full Mock Reflection And Voice")]
        public static void Run()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().isDirty||SceneManager.GetActiveScene().path!=CreationSceneAssembler.ScenePath)throw new InvalidOperationException("Open saved creation scene outside Play Mode.");
            SessionState.SetBool(Running,true);EditorApplication.isPlaying=true;
        }
        private static void Begin()
        {
            volume=AudioListener.volume;
            try {
                director=One<ReflectionExperienceDirector>();creation=One<CreationSessionDirector>();presenter=One<CreationSessionPresenter>();backend=One<TripoModelLoader>();voice=One<RabbitVoiceNarrator>();microphone=One<ReflectionMicrophoneInput>();reveal=One<ParticleModelReveal>();
                Require(director.IsConfigured,"All reflection, authored controls and original scene references assigned");
                foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true))Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0,"Missing script: "+t.name);
                var comfort=new SerializedObject(One<VRComfortSettings>());comfort.FindProperty("forceDesktopSimulation").boolValue=true;comfort.ApplyModifiedPropertiesWithoutUndo();
                step=frames=inhale=exhale=0;checking=heard=surface=false;errors="";Time.timeScale=8;AudioListener.volume=0;deadline=EditorApplication.timeSinceStartup+240;
                One<GuidedBreathingProvider>().Signal+=signal=>{if(signal.Phase==BreathPhase.Inhale)inhale++;if(signal.Phase==BreathPhase.Exhale)exhale++;};
                Application.logMessageReceived+=Log;File.WriteAllText(Report,"Running full MOCK reflection, voice tone fixture and WAV transcription transport; no real provider calls and no native microphone recording.\n");
            }catch(Exception e){Finish(e.ToString());}
        }
        private static void Log(string message,string trace,LogType type)
        {
            if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&!trace.Contains("Unity.AI.")&&!message.Contains("generators.ai.unity.com"))errors+=message+"\n";
        }
        private static void Set(Object target,string field,string value){var so=new SerializedObject(target);so.FindProperty(field).stringValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
        [Serializable]private sealed class Capability{[SerializeField]private bool liveGenerationEnabled;[SerializeField]private Budget generationBudget;public bool Enabled=>liveGenerationEnabled;public int Used=>generationBudget.Used;}
        [Serializable]private sealed class Budget{[SerializeField]private int used;public int Used=>used;}
        [Serializable]private sealed class Transcript{[SerializeField]private string text;public string Text=>text;}
        private static async void Tick()
        {
            const string restore="Library/HealSphereReflectionRestore.request";
            if(File.Exists(restore)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling){File.Delete(restore);EditorSceneManager.OpenScene(CreationSceneAssembler.ScenePath);}
            if(File.Exists(Marker)&&!EditorApplication.isPlayingOrWillChangePlaymode&&!EditorApplication.isCompiling){try{File.Delete(Marker);Run();}catch(Exception e){File.WriteAllText(Report,e.ToString());}}
            if(!EditorApplication.isPlaying||!SessionState.GetBool(Running,false)||checking)return;
            try {
                var diagnostic = step + ": " + director?.Phase + ", creation=" + creation?.State + ", models=" + creation?.CompletedCount + ", microphone=" + microphone?.State;
                if(diagnostic!=lastDiagnostic){lastDiagnostic=diagnostic;File.AppendAllText(Report,diagnostic+"\n");}
                if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Reflection verification timed out at " + diagnostic);
                if(director!=null){Require(director.Phase!=ReflectionPhase.Error,"Reflection error: "+Message.text);heard|=voice.IsSpeaking;Message.ForceMeshUpdate();Require(!Message.isTextOverflowing,"Guidance/subtitles fit at phase "+director.Phase);}
                if(step<8&&director==null)return;
                if(director!=null&&director.Phase==ReflectionPhase.Revealing&&creation.State==CreationSessionState.RevealingCreation){surface|=reveal.SurfaceSamplingAvailable;Require(reveal.SurfaceSamplingAvailable,"Actual GLB mesh surface sampled");}
                if(step==0&&++frames>20){
                    Require(director.Phase==ReflectionPhase.Welcome,"No automatic paid task/narration on entering Play Mode");
                    // Pin only after all Start methods have read the user's real device settings. No user file is modified.
                    Set(backend,"backendUrl","http://127.0.0.1:3347");Set(backend,"backendAccessToken","healsphere-reflection-mock-only-0123456789");
                    typeof(TripoModelLoader).GetProperty("LiveConnectionRequested").SetValue(backend,true);
                    checking=true;await CheckBudget();checking=false;Button("PauseButton").onClick.Invoke();step=1;
                }else if(step==1&&director.Phase==ReflectionPhase.Animal){
                    Require(inhale>0&&exhale>0,"Initial configurable inhale/exhale cycles complete");
                    checking=true;await TranscribeIntoField();checking=false;Require(presenter.RequestText=="fox","Animal transcription displayed before confirmation");
                    Button("PauseButton").onClick.Invoke();step=2;
                }else if(step==2&&director.Phase==ReflectionPhase.Thought1){
                    checking=true;await TranscribeIntoField();checking=false;presenter.SetRequestText("I would like to spend more time with my friends.");Button("PauseButton").onClick.Invoke();
                    Require(director.Thought1.Contains("more time"),"Recognized text can be edited before submission");
                    Button("RestartButton").onClick.Invoke();Require(presenter.RequestText==director.Thought1,"Back restores earlier answer");Button("PauseButton").onClick.Invoke();step=3;
                }else if(step==3&&director.Phase==ReflectionPhase.Thought2){
                    checking=true;await TranscribeIntoField();checking=false;Button("PauseButton").onClick.Invoke();Require(director.Phase==ReflectionPhase.Review,"All three inputs collected before processing");
                    Button("PauseButton").onClick.Invoke();step=4;
                }else if(step==4&&director.Phase==ReflectionPhase.Complete){
                    Require(surface&&creation.CompletedCount==3,"Three distinct saved GLBs complete the same particle/reveal pipeline");
                    Require(Object.FindObjectsByType<RuntimeModelOwnership>().Length==3,"Exactly three retained imported models");
                    Require(heard,"Rabbit AudioSource plays returned WAV narration");
                    foreach(var model in Object.FindObjectsByType<RuntimeModelOwnership>())foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))Require(renderer.sharedMaterials.All(m=>m!=null&&m.shader.isSupported&&!m.shader.name.Contains("Gentle")),"Supported original materials restored");
                    Require(Object.FindObjectsByType<ParticleSystem>().Single(p=>p.name=="Creation Surface Motes").particleCount==0,"Surface motes cleaned up");
                    Message.ForceMeshUpdate();Require(!Message.isTextOverflowing,"Closing subtitles fit authored rectangle");
                    ScreenCapture.CaptureScreenshot("Library/HealSphereReflectionCompleted.png");frames=0;step=5;
                }else if(step==5&&++frames>5){
                    checking=true;await CheckBudget();checking=false;
                    Button("PauseButton").onClick.Invoke();Require(creation.CompletedCount==0&&director.Thought1==""&&director.Thought2==""&&presenter.RequestText=="","Restart clears models, input field and private session answers");
                    Button("PauseButton").onClick.Invoke();Button("PauseButton").onClick.Invoke();Require(director.Phase==ReflectionPhase.Animal,"Skip introduction without requiring rigid breathing");
                    typeof(ReflectionMicrophoneInput).GetProperty("State").SetValue(microphone,MicrophoneInputState.Recording);
                    Button("RestartButton").onClick.Invoke();Require(microphone.State==MicrophoneInputState.Idle&&director.Phase==ReflectionPhase.Welcome,"Back cancels recording state without accessing a physical microphone");
                    Button("PauseButton").onClick.Invoke();Button("PauseButton").onClick.Invoke();
                    // Test the microphone-unavailable fallback without accessing or recording any physical device.
                    Set(backend,"backendAccessToken","");Button("MicrophoneButton").onClick.Invoke();Require(microphone.State==MicrophoneInputState.Unavailable,"Unavailable speech service leaves typing enabled");Set(backend,"backendAccessToken","healsphere-reflection-mock-only-0123456789");
                    presenter.SetRequestText("rabbit");Button("PauseButton").onClick.Invoke();Button("PauseButton").onClick.Invoke();Button("PauseButton").onClick.Invoke();
                    Require(director.Phase==ReflectionPhase.Review&&director.Thought1==""&&director.Thought2=="","Both thoughts can be skipped");
                    foreach(var device in Microphone.devices)Require(!Microphone.IsRecording(device),"No unwanted native microphone recording");
                    Button("PauseButton").onClick.Invoke();step=6;
                }else if(step==6&&director.Phase==ReflectionPhase.MissingModels){
                    Require(director.IsConfigured,"Reflection must stay configured after restart and three imported models");
                    Require(microphone.State!=MicrophoneInputState.Permission&&microphone.State!=MicrophoneInputState.Transcribing&&microphone.State!=MicrophoneInputState.Recording,"No stale microphone state may block offline illustrations");
                    Set(backend,"backendUrl","http://127.0.0.1:1");Button("PauseButton").onClick.Invoke();
                    Require(director.Phase!=ReflectionPhase.MissingModels,"Use illustrations leaves the missing-model choice");
                    step=7;
                }else if(step==7&&director.Phase==ReflectionPhase.Complete){
                    Require(creation.CompletedCount==3,"Offline local illustrated GLBs load with configured backend unreachable");
                    Set(backend,"backendUrl","http://127.0.0.1:3347");checking=true;await CheckBudget();checking=false;
                    Button("WellnessButton").onClick.Invoke();step=8;
                }else if(step==8&&SceneManager.GetActiveScene().name=="HealSphereMoment"){
                    var wellness=One<ExperienceDirector>();if(wellness.Phase!=ExperiencePhase.Completed)return;
                    Require(wellness.ElapsedSeconds==120,"Preserved wellness-only timeline");Button("RestartButton").onClick.Invoke();Require(wellness.ElapsedSeconds==0,"Wellness restart");
                    Require(errors.Length==0,"Runtime errors: "+errors);Finish(null);
                }
            }catch(Exception e){Finish(e.ToString());}
        }
        private static async System.Threading.Tasks.Task CheckBudget()
        {
            using var timeout=new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
            var bytes=await backend.SendCreationRequestAsync("/api/capabilities",null,timeout.Token);var c=JsonUtility.FromJson<Capability>(Encoding.UTF8.GetString(bytes));Require(!c.Enabled&&c.Used==0,"Mock provider service paid ledger remains zero");
        }
        private static byte[] Wav()
        {
            const int rate=16000;var b=new byte[44+rate*2];
            void Text(int offset,string text)=>Encoding.ASCII.GetBytes(text).CopyTo(b,offset);
            void Int(int offset,int value)=>BitConverter.GetBytes(value).CopyTo(b,offset);
            void Short(int offset,short value)=>BitConverter.GetBytes(value).CopyTo(b,offset);
            Text(0,"RIFF");Int(4,b.Length-8);Text(8,"WAVEfmt ");Int(16,16);Short(20,1);Short(22,1);Int(24,rate);Int(28,rate*2);Short(32,2);Short(34,16);Text(36,"data");Int(40,rate*2);
            for(int i=0;i<rate;i++)Short(44+i*2,(short)(Math.Sin(i/(double)rate*440*Math.PI*2)*300));return b;
        }
        private static async System.Threading.Tasks.Task TranscribeIntoField()
        {
            using var timeout=new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));var bytes=await backend.TranscribeSpeechAsync(Wav(),timeout.Token);
            var result=JsonUtility.FromJson<Transcript>(Encoding.UTF8.GetString(bytes));presenter.SetRequestText(result.Text);
        }
        private static void Require(bool condition,string text){if(!condition)throw new InvalidOperationException(text);}
        private static void Finish(string error)
        {
            Application.logMessageReceived-=Log;Time.timeScale=1;AudioListener.volume=volume;checking=false;
            File.AppendAllText(Report,error??"PASS: configured authored UI, optional intro breathing, three mock WAV transcriptions with editable text, back/edit/skip thoughts, privacy-safe structured plan, three saved distinct same-animal GLBs through glTFast and mesh-surface reveal, original materials, returned narration playback, readable subtitles, restart clears private input and models, unavailable microphone-service typed fallback, Back cancels simulated recording state, generation-disabled missing-model choice, three local rabbit illustrations complete with backend unreachable, no native recording, zero paid tasks, preserved wellness mode and restart. Native microphone/Android permission and real AI voice quality remain manual checks.\n");
            SessionState.SetBool(Running,false);director=null;EditorApplication.isPlaying=false;File.WriteAllText("Library/HealSphereReflectionRestore.request","Restore saved scene after temporary test settings.");
        }
    }
}
