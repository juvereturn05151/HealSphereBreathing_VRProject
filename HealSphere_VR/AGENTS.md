# AGENTS.md — Breathe Into Being (Unity VR)

These rules apply to all AI coding agents working on the Unity project. Follow them for every new script, prefab, scene, and modification unless the project owner explicitly overrides them.

## 1. Core rules (mandatory)

1. **Do not create UI through C# code.** Do not instantiate or construct Canvas, Panels, Buttons, TextMeshPro elements, sliders, layouts, UI Toolkit trees, or other interface elements at runtime or in editor scripts. Do not use `OnGUI` for the game UI.
2. **Author UI visually in the Unity Editor** as scene objects or reusable prefabs. C# scripts may reference those existing UI components, update text/visual state, bind listeners, and show or hide them. Never create missing UI as a fallback in code.
3. **Use `[SerializeField] private` for Inspector-assigned fields**, not `public` fields. Preserve encapsulation. Prefer `[SerializeField] private GameObject dreamSeed;` over `public GameObject dreamSeed;`.
4. **Do not change fields to `public` just to access them from another script.** Use properties with private setters, explicit methods, events, or narrowly scoped interfaces when access is required.
5. **Never silently overwrite scenes, prefabs, serialized references, input actions, or project settings.** Inspect existing assets first and preserve references.

## 2. UI authoring policy

- Create and arrange all UI components manually in the Unity Editor, including world-space VR canvases, text, buttons, icons, progress indicators, breathing prompts, subtitle panels, and reflection panels.
- Create reusable prefabs for: `CompanionDialoguePanel`, `BreathingGuide`, `ObjectChoicePanel`, `CreationProgressPanel`, and `ReflectionPanel` as needed.
- Scripts should be **controllers/presenters for existing components only**, e.g. `dialogueText.text = message`, `panelRoot.SetActive(true)`, or `progressImage.fillAmount = progress`.
- Expose required UI dependencies as `[SerializeField] private` fields for drag-and-drop assignment in the Inspector.
- If UI is missing, **stop and describe the required hierarchy and Inspector hookups**. Do not generate it in C# and do not claim the feature is complete.
- Runtime 3D gameplay objects (such as generated models and particles) may be instantiated when required by gameplay. This exception does **not** apply to UI.
- Avoid UI that is attached rigidly to the player's head. Prefer comfortable world-space UI at readable distances; account for seated Quest use.

## 3. C# conventions

```csharp
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CompanionDialoguePresenter : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private Button continueButton;

    public void ShowMessage(string message)
    {
        if (panelRoot == null || dialogueText == null)
        {
            Debug.LogError("CompanionDialoguePresenter: assign the UI references in the Inspector.", this);
            return;
        }

        dialogueText.text = message;
        panelRoot.SetActive(true);
    }
}
```

- Default fields to `private`; use `[SerializeField]` only when values or references need Inspector configuration.
- Constants, methods, and properties may be `public` when genuinely part of a documented API. The rule against `public` applies to mutable serialized data fields, not every public member.
- Use `[Header]`, `[Tooltip]`, `[Range]`, and validation in `OnValidate` where they improve Inspector usability.
- Use `RequireComponent` when useful, but don't automatically add UI elements.
- Cache component references and avoid frequent `FindObjectOfType` / `GameObject.Find` calls.
- Prefer clear modular scripts, events, and dependency injection via Inspector over monolithic managers.
- Keep existing code style and namespaces where established.

## 4. VR and wellness requirements

- Target the project's existing Unity version, URP, OpenXR, and XR Interaction Toolkit configuration; inspect before changing packages.
- Favor seated/stationary experiences, stable camera, no forced locomotion, comfortable distances, restrained particle counts, gentle transitions, and adjustable audio.
- Treat controller-near-belly movement as an **unvalidated breathing proxy**, not a medical breathing sensor.
- Provide a guided/timed breathing fallback and never force breath holds or punish players for a missed cycle.
- Keep the whole experience working with pre-generated models when AI services are unavailable.

## 5. AI and generated assets

- Never place LLM or Tripo API secrets in Unity client scripts, assets, logs, or version control; call a secure backend.
- Apply content-safety checks before model generation; do not show unreviewed or potentially unsafe assets.
- Use a consistent cute, calming, family-friendly art direction for generation prompts.
- Avoid claiming to infer a player's mental health or true feelings from three creations. Reflections must be tentative, supportive, and non-diagnostic.
- Load generated 3D models asynchronously, bound memory/triangle/material usage, and recover gracefully from slow or failed API calls.

## 6. Workflow for every coding task

1. Inspect relevant existing files, scene/prefab hierarchy, packages, and conventions before changes.
2. Summarize planned changes briefly and name any UI objects that **the human must create in the Editor**.
3. Implement code using `[SerializeField] private` Inspector references. **Never build UI via scripts.**
4. Document each required Inspector assignment and any prefab/scene hookup in the handoff.
5. Test compilation and relevant behavior as far as the environment permits; clearly distinguish validated results from headset-only checks.
6. Report changed files, manual Unity Editor steps, known limitations, and next testing steps.

## 7. Definition of done

A task is complete only when code compiles (when testable), project assets remain intact, serialized fields are properly scoped, required UI is authored as scene/prefab objects (or documented explicitly as pending manual work), and the feature can be tested without silent UI creation or missing-reference workarounds.

**Priority reminder:** No code-generated UI. Inspector-driven references with `[SerializeField] private`, not public fields.
