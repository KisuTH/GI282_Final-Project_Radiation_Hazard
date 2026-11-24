using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace RadiationShowcase
{
    /// <summary>
    /// Survival guide + win / death + instant reset.
    /// Preparation steps can be done in any order (the board suggests one); the last step is walking through
    /// the leak to EXIT. Reaching the lethal dose kills the player. Either ending fades slowly to black, then
    /// shows the dose received and what that dose does to a human body.
    /// </summary>
    public class GameFlow : MonoBehaviour
    {
        [System.Serializable]
        public class Step
        {
            public string id;            // action id raised by an interactable ("" = reach the exit)
            public string title;
            [TextArea] public string howTo;
        }

        [Header("References")]
        public ShowcaseRig rig;
        public RadiationSource radiation;
        public ExposureEffects exposureEffects;
        public BreakableGlass glass;
        public Transform startPoint;
        public Transform exitPoint;
        public float exitRadius = 1.0f;
        [Tooltip("Geiger, axe, suit, lever, pill bottle — reset on restart")]
        public Grabbable[] interactables;
        [Tooltip("Step id each interactable belongs to (same order); used for highlighting")]
        public string[] interactableIds;
        public Text fanStatusLabel;

        [Header("UI")]
        public Text boardSteps;
        public Text boardStatus;
        [Tooltip("All Text components, so a Thai-capable OS font can be applied at runtime")]
        public Text[] allTexts;

        [Header("Ending (fade to black)")]
        public Camera playerCamera;
        public Image fadeImage;              // black image right in front of the camera
        public GameObject endPanel;          // message panel on the 'UI' layer
        public Text endText;
        public int endLayer = 5;             // UI layer: the only thing the camera shows after the fade
        public float fadeDuration = 3f;
        public float deathRestartDelay = 12f;

        [Header("Dose")]
        [Tooltip("Body dose (Sv) that kills the player. LD50 without treatment is about 3.5–5 Sv")]
        public float lethalDose = 4f;

        [Header("Protection")]
        public float suitFactor = 0.4f;
        public float pillFactor = 0.5f;

        [Header("Steps")]
        public Step[] steps =
        {
            new Step { id = "geiger", title = "หยิบ Geiger Counter จากตู้ฉุกเฉิน",
                howTo = "หยิบขวานข้างตู้ (Grip) เหวี่ยงทุบกระจกให้แตก แล้วจับเครื่องถือไว้ ค่ารังสีดูได้จากเครื่องเท่านั้น" },
            new Step { id = "suit", title = "สวมชุดป้องกันรังสี",
                howTo = "ไปที่ราวแขวนชุด กด Grip จับชุดค้างไว้ แล้วดึงมาแนบตัว" },
            new Step { id = "fan", title = "ปิดพัดลมระบายอากาศ",
                howTo = "จับหัวคันโยกสีแดงบนแผงสีฟ้า (Grip) แล้วโยกลงจนสุด" },
            new Step { id = "pill", title = "กินยา Potassium Iodide (KI)",
                howTo = "หยิบขวดยาสีส้มบนโต๊ะ แล้วยกขึ้นมาที่ปาก" },
            new Step { id = "", title = "เดินฝ่าโซนรังสีไปที่ทางออก",
                howTo = "รังสีรั่วอยู่ข้างประตู! ดูค่าจาก Geiger แล้วรีบผ่านวงแดง (กดสติ๊กซ้าย = วิ่ง) ไปที่ EXIT" },
        };

        bool[] done;
        float elapsed;
        bool finished;
        float statusTimer;
        int originalMask;
        Coroutine endRoutine;
        const string DefaultStatus = "ค่ารังสีดูได้จาก Geiger Counter เท่านั้น  ยิ่งคลิกถี่ = ยิ่งใกล้จุดรั่ว  ถ้าตาเริ่มมืด = ได้รับรังสีมากแล้ว";
        AudioSource sfx;
        AudioClip stepClip, winClip, errorClip, deathClip;

        void Awake()
        {
            done = new bool[steps.Length];
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;
            stepClip = ProceduralAudio.Notes("StepDone", new[] { 880f, 1320f }, 0.12f, 0.5f);
            winClip = ProceduralAudio.Notes("Win", new[] { 660f, 880f, 1100f, 1320f }, 0.14f, 0.5f);
            errorClip = ProceduralAudio.Notes("Error", new[] { 300f, 220f }, 0.15f, 0.5f);
            deathClip = ProceduralAudio.Notes("Death", new[] { 220f, 196f, 165f, 110f }, 0.35f, 0.5f);
            if (playerCamera != null) originalMask = playerCamera.cullingMask;
            ApplyThaiFont();
        }

        void OnEnable()
        {
            WorldButton.Pressed += OnButtonPressed;
            ShowcaseEvents.ActionDone += OnActionDone;
            if (rig != null) rig.ResetRequested += ResetGame;
        }

        void OnDisable()
        {
            WorldButton.Pressed -= OnButtonPressed;
            ShowcaseEvents.ActionDone -= OnActionDone;
            if (rig != null) rig.ResetRequested -= ResetGame;
        }

        void Start() => ResetGame();

        public bool IsFinished => finished;

        /// <summary>The default UI font has no Thai glyphs, so use a Windows Thai font at runtime.</summary>
        void ApplyThaiFont()
        {
            var font = Font.CreateDynamicFontFromOSFont(
                new[] { "Leelawadee UI", "Leelawadee", "Tahoma", "Segoe UI", "Arial" }, 64);
            if (font == null || allTexts == null) return;
            foreach (var t in allTexts)
                if (t != null) t.font = font;
        }

        // ---------------------------------------------------------------- flow

        public void ResetGame()
        {
            if (endRoutine != null) { StopCoroutine(endRoutine); endRoutine = null; }
            for (int i = 0; i < done.Length; i++) done[i] = false;
            elapsed = 0f;
            finished = false;

            if (rig != null) rig.ReleaseAll();
            if (glass != null) glass.ResetGlass();
            if (interactables != null)
                foreach (var g in interactables) if (g != null) g.ResetItem();
            Physics.SyncTransforms();   // physics sees the reset positions immediately
            if (radiation != null) radiation.ResetState();
            if (exposureEffects != null) exposureEffects.ResetEffects();
            if (rig != null) { rig.SetGloves(false); if (startPoint != null) rig.ResetTo(startPoint); }

            if (playerCamera != null) playerCamera.cullingMask = originalMask;
            SetFade(0f);
            if (endPanel != null) endPanel.SetActive(false);
            SetFanLabel(true);
            RefreshBoard();
            SetStatus(DefaultStatus, 0f);
        }

        void OnButtonPressed(WorldButton button)
        {
            if (button.id == "reset") ResetGame();
        }

        void OnActionDone(string id)
        {
            if (finished) return;
            int index = System.Array.FindIndex(steps, s => s.id == id);
            if (index < 0 || done[index]) return;
            done[index] = true;

            switch (id)
            {
                case "geiger":
                    SetStatus("ได้ Geiger Counter แล้ว ถือไว้ตลอด หน้าจอบอกอัตรารังสี (บน) และรังสีที่ร่างกายได้รับสะสม (DOSE)", 4f);
                    break;
                case "suit":
                    rig.SetGloves(true);
                    SetStatus("สวมชุดป้องกันรังสีแล้ว ร่างกายได้รับรังสีน้อยลง ~60%", 3f);
                    break;
                case "fan":
                    if (radiation != null) radiation.fanOn = false;
                    SetFanLabel(false);
                    SetStatus("ปิดพัดลมแล้ว รังสีกระจายน้อยลง ~70% (ลองดูค่าบน Geiger)", 3f);
                    break;
                case "pill":
                    SetStatus("กินยา KI แล้ว ไทรอยด์ไม่ดูดไอโอดีนกัมมันตรังสี (ในเกม: ลดรังสี ~50%)", 3f);
                    break;
            }

            UpdateProtection();
            sfx.PlayOneShot(stepClip);
            RefreshBoard();
        }

        void UpdateProtection()
        {
            if (radiation == null) return;
            float p = 1f;
            if (IsDone("suit")) p *= suitFactor;
            if (IsDone("pill")) p *= pillFactor;
            radiation.protection = p;
        }

        bool IsDone(string id)
        {
            int i = System.Array.FindIndex(steps, s => s.id == id);
            return i >= 0 && done[i];
        }

        int FirstUndone()
        {
            for (int i = 0; i < steps.Length; i++) if (!done[i]) return i;
            return steps.Length;
        }

        bool PrepDone()
        {
            for (int i = 0; i < steps.Length; i++)
                if (!string.IsNullOrEmpty(steps[i].id) && !done[i]) return false;
            return true;
        }

        // ---------------------------------------------------------------- endings

        void Win()
        {
            for (int i = 0; i < done.Length; i++) done[i] = true;
            RefreshBoard();
            sfx.PlayOneShot(winClip);
            endRoutine = StartCoroutine(EndSequence(false));
        }

        void Die()
        {
            sfx.PlayOneShot(deathClip);
            endRoutine = StartCoroutine(EndSequence(true));
        }

        IEnumerator EndSequence(bool died)
        {
            finished = true;
            if (radiation != null) radiation.accumulate = false;   // freeze the dose for the report
            float dose = radiation != null ? radiation.TotalDose : 0f;

            // 1) slow fade to black
            for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
            {
                SetFade(t / fadeDuration);
                yield return null;
            }
            SetFade(1f);

            // 2) only the message stays visible (the camera clears to black), with no dark edges over it
            if (exposureEffects != null) exposureEffects.Suppress(true);
            if (playerCamera != null) playerCamera.cullingMask = 1 << endLayer;
            SetFade(0f);
            if (endPanel != null)
            {
                Transform head = rig.head;
                Vector3 fwd = rig.FlatForward;
                endPanel.transform.SetPositionAndRotation(head.position + fwd * 1.3f, Quaternion.LookRotation(fwd, Vector3.up));
                endPanel.SetActive(true);
            }

            // 3) message (death: auto restart after a countdown)
            if (!died)
            {
                SetEndText(false, dose, 0f);
                yield break;                                     // wait for B / R
            }
            for (float left = deathRestartDelay; left > 0f; left -= Time.deltaTime)
            {
                SetEndText(true, dose, left);
                yield return null;
            }
            ResetGame();
        }

        void SetEndText(bool died, float dose, float secondsLeft)
        {
            if (endText == null) return;
            string doseColor = "#" + ColorUtility.ToHtmlStringRGB(RadiationEffects.LevelColor(dose));
            int m = (int)(elapsed / 60f), s = (int)(elapsed % 60f);
            string effects = RadiationEffects.Describe(dose);

            if (died)
            {
                endText.text =
                    "<size=96><color=#FF4A3A>เสียชีวิต</color></size>\n\n" +
                    $"ร่างกายได้รับรังสี <color={doseColor}><b>{RadiationEffects.Dose(dose)}</b></color>  (= {dose * 1000f:0} mSv)\n" +
                    $"เกินระดับอันตรายถึงชีวิตของเกมนี้ ({lethalDose:0} Sv)\n\n" +
                    $"<color=#FFD54A>ผลต่อร่างกาย:</color> {effects}\n\n" +
                    "<size=34><color=#BBBBBB>ครั้งหน้า: ใส่ชุด กินยา KI ปิดพัดลม แล้วรีบผ่านจุดรั่ว อย่าอยู่ใกล้นาน</color></size>\n" +
                    $"<size=36><color=#FFD54A>เริ่มใหม่อัตโนมัติใน {Mathf.CeilToInt(secondsLeft)} วินาที</color></size>";
            }
            else
            {
                endText.text =
                    "<size=96><color=#5CFF8A>รอดชีวิต / ปลอดภัย</color></size>\n\n" +
                    $"ออกจากพื้นที่รังสีรั่วไหลได้ในเวลา {m:00}:{s:00}\n" +
                    $"ร่างกายได้รับรังสีรวม <color={doseColor}><b>{RadiationEffects.Dose(dose)}</b></color>  (= {dose * 1000f:0.#} mSv)\n\n" +
                    $"<color=#FFD54A>ผลต่อร่างกาย:</color> {effects}\n\n" +
                    "<size=34><color=#BBBBBB>เลือกเมนูด้านล่าง (VR: ชี้แล้วกด Trigger | PC: คลิก)</color></size>";
            }
        }

        void SetFade(float a)
        {
            if (fadeImage == null) return;
            var c = fadeImage.color; c.a = a;
            fadeImage.color = c;
            fadeImage.enabled = a > 0.001f;
        }

        // ---------------------------------------------------------------- update

        void Update()
        {
            if (finished) return;
            elapsed += Time.deltaTime;

            if (radiation != null && radiation.TotalDose >= lethalDose) { Die(); return; }

            if (rig != null && exitPoint != null)
            {
                Vector3 a = rig.head.position; a.y = 0f;
                Vector3 b = exitPoint.position; b.y = 0f;
                if (Vector3.Distance(a, b) < exitRadius)
                {
                    if (PrepDone()) { Win(); return; }
                    if (statusTimer <= 0f)
                    {
                        SetStatus($"ประตูยังไม่เปิด! เตรียมตัวไม่ครบ – ทำ Step {FirstUndone() + 1} ก่อน (และอย่ายืนในโซนรังสีนาน!)", 3f);
                        sfx.PlayOneShot(errorClip);
                    }
                }
            }

            if (statusTimer > 0f)
            {
                statusTimer -= Time.deltaTime;
                if (statusTimer <= 0f) SetStatus(DefaultStatus, 0f);
            }

            UpdateHighlights();
        }

        void UpdateHighlights()
        {
            if (interactables == null || interactableIds == null) return;
            int current = FirstUndone();
            string currentId = current < steps.Length ? steps[current].id : null;
            // the axe belongs to the Geiger step
            for (int i = 0; i < interactables.Length && i < interactableIds.Length; i++)
                if (interactables[i] != null)
                    interactables[i].highlighted = interactableIds[i] == currentId &&
                                                   !(interactables[i] is GeigerCounter gc && gc.locked);
        }

        void RefreshBoard()
        {
            if (boardSteps == null) return;
            int current = FirstUndone();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < steps.Length; i++)
            {
                if (done[i])
                    sb.Append($"<color=#5CFF8A>Step {i + 1}: {steps[i].title}  (เสร็จแล้ว)</color>\n");
                else if (i == current)
                    sb.Append($"<color=#FFD54A>> Step {i + 1}: {steps[i].title}</color>\n<size=32>   {steps[i].howTo}</size>\n");
                else
                    sb.Append($"<color=#999999>Step {i + 1}: {steps[i].title}</color>\n");
                sb.Append('\n');
            }
            boardSteps.text = sb.ToString();
        }

        void SetFanLabel(bool on)
        {
            if (fanStatusLabel == null) return;
            fanStatusLabel.text = on ? "<color=#FF5040>พัดลม\nON</color>" : "<color=#60FF90>พัดลม\nOFF</color>";
        }

        void SetStatus(string msg, float seconds)
        {
            if (boardStatus != null) boardStatus.text = msg;
            statusTimer = seconds;
        }
    }
}
