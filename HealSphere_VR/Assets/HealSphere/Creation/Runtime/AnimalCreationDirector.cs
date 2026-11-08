using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HealSphere.Breathing;
using HealSphere.Tripo;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace HealSphere.Creation
{
    [DefaultExecutionOrder(-40)]
    public sealed class AnimalCreationDirector : MonoBehaviour
    {
        [SerializeField] private CreationSessionDirector creation;
        [SerializeField] private CreationSessionPresenter presenter;
        [SerializeField] private TripoModelLoader backend;
        [SerializeField] private GuidedBreathingProvider breathing;
        [SerializeField] private Button[] choices;
        [SerializeField] private Button createButton, moreButton;
        [SerializeField] private CanvasGroup panel;
        [SerializeField] private Image panelImage;
        [SerializeField] private RabbitVoiceNarrator narrator;
        [SerializeField] private AnimalAwakening awakeningPrefab;
        [SerializeField, Range(5f, 10f)] private float quietSeconds = 8f;

        private static readonly string[] Common = { "Cat", "Dog", "Fox", "Rabbit", "Deer", "Owl" };
        private static readonly string[] More = { "Elephant", "Gorilla", "Red panda", "Dragon", "Turtle", "Bird" };
        private const string FinalBreath = "Your little friend is resting. Let’s take one last gentle breath together.";
        private const string Awakes = "Oh, look at that… Your breath gave life to your little friend!";
        private const string Wonder = "You created something wonderful, just by taking a moment to breathe.";
        private enum Phase { Choose, Prepare, Reveal, Invitation, FinalBreath, Awakening, Quiet, Complete, Friends, Recover }
        private Phase phase;
        private bool extra, demo, demoCat, offlineModel;
        private string selected = "", request = "", proposal = "", breath = "", notice = "";
        private UnityAction[] choiceActions;
        private bool editing;
        private AnimalAwakening awakening;
        private CancellationTokenSource ending;
        private string endingCaption = "";
        private int friendIndex;

        [Serializable] private sealed class PrepareRequest
        {
            [SerializeField] private string animal;
            public PrepareRequest(string value) => animal = value;
        }
        [Serializable] private sealed class Prepared
        {
            [SerializeField] private string proposal_id, modelEndpoint;
            [SerializeField] private bool prebuilt;
            public string Proposal => proposal_id;
            public string Endpoint => modelEndpoint;
            public bool Prebuilt => prebuilt;
        }
        [Serializable] private sealed class Submit
        {
            [SerializeField] private string proposal_id, idempotency_key;
            [SerializeField] private string mode = "live", confirm = "CREATE_ONE_MODEL";
            public Submit(string id) => proposal_id = idempotency_key = id;
        }
        [Serializable] private sealed class Job
        {
            [SerializeField] private string id, status, model_source;
            public string Id => id;
            public string Status => status;
            public bool Prebuilt => model_source == "prebuilt";
        }

        private bool Configured => creation != null && creation.IsConfigured && presenter != null &&
            backend != null && breathing != null && choices != null && choices.Length == Common.Length &&
            Array.TrueForAll(choices, button => button != null) && createButton != null && moreButton != null &&
            panel != null && panelImage != null;

        private void Awake() => creation?.SetExternalControl(true);

        private void Start()
        {
            if (!Configured)
            {
                Debug.LogError("Assign the visually authored animal picker controls and existing creation systems.", this);
                return;
            }
            backend.ReloadLocalConnection();
            if (narrator == null) narrator = GetComponent<RabbitVoiceNarrator>();
            if (awakeningPrefab == null)
                awakeningPrefab = Resources.Load<GameObject>("HealSphereAnimalAwakening")?.GetComponent<AnimalAwakening>();
            choiceActions = new UnityAction[choices.Length];
            for (int i = 0; i < choices.Length; i++)
            {
                int index = i;
                choiceActions[i] = () => Select(index);
                choices[i].onClick.AddListener(choiceActions[i]);
            }
            createButton.onClick.AddListener(Create);
            moreButton.onClick.AddListener(ToggleMore);
            presenter.RequestInput.onValueChanged.AddListener(Typed);
            breathing.Signal += OnBreath;
            breathing.ExhaleCompleted += OnFinalExhale;
            if (presenter.RequestInput.placeholder is TMP_Text placeholder)
                placeholder.text = "Or type your own animal…";
            presenter.RequestInput.characterLimit = 120;
            presenter.CloseKeyboard();
            Render();
        }

        private void Select(int index)
        {
            if (phase == Phase.Complete)
            {
                if (index == 0) Back();
                if (index == 1)
                {
                    friendIndex = creation.CompletedCount - 1;
                    phase = Phase.Friends;
                    Render();
                }
                return;
            }
            if (phase == Phase.Friends)
            {
                if (index == 0) Back();
                if (index == 1)
                {
                    friendIndex = (friendIndex + 1) % creation.CompletedCount;
                    creation.ShowFriend(friendIndex);
                    Render();
                }
                return;
            }
            if (phase == Phase.Recover)
            {
                if (index == 0) Begin(false);
                if (index == 1) Begin(true);
                if (index == 2) Back();
                return;
            }
            if (phase != Phase.Choose) return;
            selected = (extra ? More : Common)[index];
            editing = true;
            presenter.ClearRequest();
            presenter.CloseKeyboard();
            editing = false;
            notice = "";
            Render();
        }

        private void Typed(string value)
        {
            if (phase != Phase.Choose || editing) return;
            selected = "";
            notice = "";
            Render();
        }

        private void ToggleMore()
        {
            if (phase != Phase.Choose) return;
            extra = !extra;
            Render();
        }

        private void Create()
        {
            if (phase != Phase.Choose) return;
            string typed = presenter.RequestText.Trim();
            request = typed.Length == 0 ? selected : typed;
            if (!Regex.IsMatch(request, @"^[a-zA-Z][a-zA-Z0-9 .'\-]{0,119}$"))
            {
                notice = "Choose a preset or enter one gentle animal.";
                Render();
                return;
            }
            proposal = "";
            Begin(false);
        }

        private void Begin(bool useDemo)
        {
            StopEnding();
            ending = new CancellationTokenSource();
            if (!useDemo && narrator != null) _ = narrator.PrepareEndingAsync(ending.Token);
            demo = useDemo;
            offlineModel = demoCat = false;
            breath = "";
            phase = Phase.Prepare;
            panel.alpha = .18f;
            Render();
            creation.BeginAnimalCreation(useDemo ? "Offline animal illustration" : "Little friend", Load);
        }

        private async Task<(byte[] Bytes, bool Prebuilt)> Load(CancellationToken token)
        {
            string plain = request.Trim().ToLowerInvariant().TrimEnd('.');
            var animal = Regex.Match(plain, @"\b(fox|cat|rabbit|dog|elephant|gorilla|deer|owl)\b").Value;
            bool supported = Regex.IsMatch(plain, @"^(?:a |an )?(?:little |cute )*(?:(sleeping|seated|sitting|standing) )?(fox|cat|rabbit|dog|elephant|gorilla|deer|owl)$");
            async Task<(byte[] Bytes, bool Prebuilt)> LoadOffline()
            {
                offlineModel = true;
                demoCat = animal.Length == 0;
                Render();
                if (demoCat) return (await backend.ReadLocalDemoAsync(token), true);
                string role = plain.Contains("sleeping") ? "reflection" : plain.Contains("standing") ? "hope" : "support";
                return (await backend.ReadReflectionDemoAsync(animal, role, token), true);
            }
            if (demo) return await LoadOffline();
            if (!backend.LiveConnectionRequested || !backend.HasBackendConnection)
            {
                if (supported) return await LoadOffline();
                throw new InvalidOperationException("Backend: CONNECTION_UNAVAILABLE");
            }
            if (string.IsNullOrEmpty(proposal))
            {
                Prepared prepared;
                try
                {
                    prepared = Decode<Prepared>(await backend.PrepareAnimalAsync(JsonUtility.ToJson(new PrepareRequest(request)), token));
                }
                catch (InvalidOperationException e) when (supported && e.Message == "Backend: CONNECTION_UNAVAILABLE")
                {
                    return await LoadOffline();
                }
                if (!string.IsNullOrEmpty(prepared.Endpoint))
                    return (await backend.SendReflectionRequestAsync(prepared.Endpoint, null, token), prepared.Prebuilt);
                if (!Guid.TryParse(prepared.Proposal, out _)) throw new InvalidOperationException();
                proposal = prepared.Proposal;
            }
            var job = Decode<Job>(await backend.SendCreationRequestAsync("/api/creations", JsonUtility.ToJson(new Submit(proposal)), token));
            if (!Guid.TryParse(job.Id, out _)) throw new InvalidOperationException();
            string route = "/api/creations/" + job.Id;
            while (job.Status != "ready")
            {
                if (job.Status != "generating" && job.Status != "awaiting_review") throw new InvalidOperationException();
                await Task.Delay(TimeSpan.FromSeconds(3), token);
                job = Decode<Job>(await backend.SendCreationRequestAsync(route, null, token));
            }
            return (await backend.SendCreationRequestAsync(route + "/model", null, token), job.Prebuilt);
        }

        private static T Decode<T>(byte[] bytes) where T : class =>
            JsonUtility.FromJson<T>(Encoding.UTF8.GetString(bytes)) ?? throw new InvalidOperationException();

        private void OnBreath(BreathSignal signal)
        {
            if ((phase != Phase.Prepare && phase != Phase.FinalBreath) || signal.Phase == BreathPhase.Rest) return;
            if (phase == Phase.FinalBreath && awakening != null)
                awakening.SetBreath(signal.Phase == BreathPhase.Inhale ? signal.Progress : 1f - signal.Progress);
            string next = signal.Phase == BreathPhase.Inhale ? "Breathe in…" : "Breathe out…";
            if (next == breath) return;
            breath = next;
            Render();
        }

        private IEnumerator InviteFinalBreath()
        {
            var model = creation.LatestAnimal;
            if (awakeningPrefab != null && model != null)
            {
                awakening = Instantiate(awakeningPrefab, model.transform.position, Quaternion.identity);
                awakening.transform.SetParent(model.transform, true);
                awakening.Rest(model.transform, presenter.Viewer);
            }
            phase = Phase.Invitation;
            yield return Narrate("animal_final_breath", FinalBreath, 2.5f);
            phase = Phase.FinalBreath;
            breath = "Breathe in…";
            Render();
            breathing.Begin();
        }

        private void OnFinalExhale()
        {
            if (phase != Phase.FinalBreath) return;
            phase = Phase.Awakening;
            breathing.Stop();
            if (awakening != null) awakening.Wake();
            creation.PlayAwakeningChime();
            StartCoroutine(EnjoyAwakening());
        }

        private IEnumerator EnjoyAwakening()
        {
            yield return Narrate("animal_awakes", Awakes, 3.5f);
            yield return new WaitForSeconds(1.1f);
            yield return Narrate("animal_wonder", Wonder, 3.5f);
            phase = Phase.Quiet;
            Render();
            yield return new WaitForSeconds(quietSeconds);
            phase = Phase.Complete;
            Render();
        }

        private IEnumerator Narrate(string cue, string line, float seconds)
        {
            endingCaption = line;
            Render();
            var playback = narrator == null ? Task.CompletedTask : narrator.SpeakAsync(cue, line, "", ending.Token, cachedOnly: true);
            yield return new WaitForSeconds(seconds);
            while (!playback.IsCompleted) yield return null;
        }

        private void StopEnding()
        {
            StopAllCoroutines();
            ending?.Cancel();
            ending?.Dispose();
            ending = null;
            narrator?.StopSpeech();
        }

        private void Update()
        {
            if (choiceActions == null) return;
            panel.alpha = Mathf.MoveTowards(panel.alpha, 1f, Time.unscaledDeltaTime * 2.5f);
            if (phase != Phase.Prepare && phase != Phase.Reveal) return;
            if (creation.State == CreationSessionState.CreationComplete)
            {
                StartCoroutine(InviteFinalBreath());
                return;
            }
            Phase next = creation.State switch
            {
                CreationSessionState.ErrorOrOfflineFallback => Phase.Recover,
                CreationSessionState.RevealingCreation => Phase.Reveal,
                _ => phase
            };
            if (next == phase) return;
            phase = next;
            Render();
        }

        private void Back()
        {
            StopEnding();
            creation.ShowFriend(-1);
            phase = Phase.Choose;
            selected = notice = "";
            presenter.ClearRequest();
            Render();
        }

        private void Render()
        {
            if (!Configured) return;
            bool choosing = phase == Phase.Choose;
            string offline = offlineModel ? (demoCat ? "\n<size=23>Offline demo · prepared cat</size>" : "\n<size=23>Offline animal illustration</size>") : "";
            string message = phase switch
            {
                Phase.Choose => "<size=54><b>Create an Animal</b></size>\n<size=30>Choose an animal, or imagine your own.</size>\n<size=25>" +
                    (notice.Length > 0 ? notice : selected.Length > 0 ? "Selected · " + selected : " ") + "</size>",
                Phase.Prepare => "<size=40>" + (breath.Length == 0 ? "Preparing your little friend…" : breath) +
                    "</size>\n<size=27>Your little friend is growing.</size>" + offline,
                Phase.Reveal => "<size=40>Your little friend is taking shape…</size>" + offline,
                Phase.Invitation or Phase.Awakening => "<size=34>" + endingCaption + "</size>" + offline,
                Phase.FinalBreath => "<size=40>" + breath + "</size>" + offline,
                Phase.Quiet or Phase.Complete => "",
                Phase.Friends => "<size=31>Little Friends · " + (friendIndex + 1) + " / " + creation.CompletedCount + "</size>",
                _ => "<size=35>" + RecoveryMessage() + "</size>\n<size=26>Try again, choose a different animal, or use a prepared illustration.</size>"
            };
            bool recover = phase == Phase.Recover;
            bool complete = phase == Phase.Complete;
            bool friends = phase == Phase.Friends;
            presenter.Present(message, "", "", choosing, false, false);
            panelImage.color = new Color(1, 1, 1, choosing ? .98f : complete || phase == Phase.Quiet ? 0f : .32f);
            string[] page = extra ? More : Common;
            for (int i = 0; i < choices.Length; i++)
            {
                if (choosing)
                {
                    int icon = extra ? (i < 2 ? i + 6 : -1) : i;
                    string label = icon < 0 ? page[i] : $"<sprite index={icon}>  {page[i]}";
                    Style(choices[i], label, page[i] == selected);
                }
                else if (i == 2 && recover) Style(choices[i], "Back", false);
                else if ((recover || complete || friends) && i < 2 && (!friends || i == 0 || creation.CompletedCount > 1))
                {
                    string label = complete || friends ? (i == 0 ? "Create Another Friend" : complete ? "Visit My Little Friends" : "Next Friend") :
                        (i == 0 ? "Try Again" : "Use Offline Demo");
                    Style(choices[i], label, recover && i == 0);
                    if (complete || friends) choices[i].GetComponentInChildren<TMP_Text>(true).fontSize = 23;
                }
                else choices[i].gameObject.SetActive(false);
            }
            createButton.gameObject.SetActive(choosing);
            moreButton.gameObject.SetActive(choosing);
            if (choosing)
            {
                Style(createButton, "Create and Breathe", true);
                Style(moreButton, extra ? "Common" : "More", false);
            }
        }

        private string RecoveryMessage() => creation.Failure switch
        {
            "Backend: CONNECTION_UNAVAILABLE" => "The creation service is unreachable.",
            "Backend: REFLECTION_TASK_AND_CREDIT_AUTHORIZATION_REQUIRED" or "Backend: GENERATION_DISABLED" =>
                "New animal generation is turned off.",
            "Backend: INSUFFICIENT_CREDITS" or "Backend: REFLECTION_AUTHORIZATION_BUDGET_EXHAUSTED" =>
                "The creation budget is unavailable.",
            "Backend: INVALID_ANIMAL" => "Try one animal, such as a sleeping fox.",
            _ => "We couldn’t load this animal."
        };

        private static void Style(Button button, string label, bool selected)
        {
            button.gameObject.SetActive(true);
            var text = button.GetComponentInChildren<TMP_Text>(true);
            text.text = label;
            text.color = new Color(.15f, .10f, .24f);
            text.fontSize = 30;
            var colors = button.colors;
            colors.normalColor = selected ? new Color(.94f, .66f, .98f) : new Color(.83f, .78f, .98f);
            colors.highlightedColor = new Color(1f, .83f, .98f);
            colors.selectedColor = colors.highlightedColor;
            colors.pressedColor = new Color(.79f, .61f, .95f);
            button.colors = colors;
        }

        private void OnDestroy()
        {
            StopEnding();
            if (choiceActions == null) return;
            for (int i = 0; i < choices.Length; i++)
                if (choices[i] != null) choices[i].onClick.RemoveListener(choiceActions[i]);
            if (createButton != null) createButton.onClick.RemoveListener(Create);
            if (moreButton != null) moreButton.onClick.RemoveListener(ToggleMore);
            if (breathing != null) breathing.Signal -= OnBreath;
            if (breathing != null) breathing.ExhaleCompleted -= OnFinalExhale;
            if (presenter == null) return;
            if (presenter.RequestInput != null) presenter.RequestInput.onValueChanged.RemoveListener(Typed);
        }
    }
}
