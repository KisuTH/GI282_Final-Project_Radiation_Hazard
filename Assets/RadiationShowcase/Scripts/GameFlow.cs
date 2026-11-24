using UnityEngine;
using UnityEngine.UI;

namespace RadiationShowcase
{
    /// <summary>
    /// Survival guide + win condition + instant reset.
    /// Steps 1-3 can be done in any order (the board suggests an order); step 4 = walk through the leak to EXIT.
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
        public Transform startPoint;
        public Transform exitPoint;
        public float exitRadius = 1.0f;
        [Tooltip("Suit, pill bottle, lever — reset on restart, highlighted when it's their step")]
        public Grabbable[] interactables;
        public string[] interactableIds;   // same order as interactables
        public Text fanStatusLabel;

        [Header("UI")]
        public Text boardSteps;
        public Text boardStatus;
        public Text hudText;
        public GameObject successPanel;
        public Text successText;
        [Tooltip("All Text components, so a Thai-capable OS font can be applied at runtime")]
        public Text[] allTexts;

        [Header("Dose limit")]
        [Tooltip("Total dose (µSv) that ends the run; the game then restarts by itself")]
        public float doseLimit = 25f;
        public float failRestartDelay = 4f;
        public Color successColor = new Color(0.02f, 0.12f, 0.06f, 0.92f);
        public Color failColor = new Color(0.25f, 0.02f, 0.02f, 0.94f);

        [Header("Protection")]
        public float suitFactor = 0.4f;
        public float pillFactor = 0.5f;

        [Header("Steps")]
        public Step[] steps =
        {
            new Step { id = "suit", title = "สวมชุดป้องกันรังสี",
                howTo = "เดินไปที่ราวแขวนชุด กด Grip จับชุดค้างไว้ แล้วดึงมาแนบตัว" },
            new Step { id = "fan", title = "ปิดพัดลมระบายอากาศ",
                howTo = "ไปที่แผงสีฟ้าบนผนัง จับปุ่มหัวคันโยกสีแดง (Grip) แล้วโยกลงจนสุด" },
            new Step { id = "pill", title = "กินยา Radiation Blocker",
                howTo = "หยิบขวดยาสีส้มบนโต๊ะ แล้วยกขึ้นมาที่ปาก" },
            new Step { id = "", title = "เดินฝ่าโซนรังสีไปที่ทางออก",
                howTo = "รังสีรั่วอยู่ข้างประตู! รีบเดิน (กดสติ๊กซ้าย = วิ่ง) ผ่านวงสีแดงไปที่ EXIT" },
        };

        bool[] done;
        float elapsed;
        bool finished, failed;
        float restartTimer;
        float statusTimer;
        const string DefaultStatus = "Geiger Counter ที่ข้อมือคลิกถี่ = ใกล้จุดรั่ว  เตรียมตัวให้ครบก่อนออกจากห้อง";
        AudioSource sfx;
        AudioClip stepClip, winClip, errorClip;

        void Awake()
        {
            done = new bool[steps.Length];
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;
            stepClip = ProceduralAudio.Notes("StepDone", new[] { 880f, 1320f }, 0.12f, 0.5f);
            winClip = ProceduralAudio.Notes("Win", new[] { 660f, 880f, 1100f, 1320f }, 0.14f, 0.5f);
            errorClip = ProceduralAudio.Notes("Error", new[] { 300f, 220f }, 0.15f, 0.5f);
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
            for (int i = 0; i < done.Length; i++) done[i] = false;
            elapsed = 0f;
            finished = false;
            failed = false;
            if (rig != null) rig.ReleaseAll();
            if (interactables != null)
                foreach (var g in interactables) if (g != null) g.ResetItem();
            if (radiation != null) { radiation.ResetState(); radiation.protection = 1f; }
            if (rig != null) { rig.SetGloves(false); if (startPoint != null) rig.ResetTo(startPoint); }
            if (successPanel != null) successPanel.SetActive(false);
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
                case "suit":
                    rig.SetGloves(true);
                    SetStatus("สวมชุดป้องกันรังสีแล้ว ลดการรับรังสีได้ ~60%", 3f);
                    break;
                case "fan":
                    if (radiation != null) radiation.fanOn = false;
                    SetFanLabel(false);
                    SetStatus("ปิดพัดลมแล้ว รังสีกระจายช้าลง", 3f);
                    break;
                case "pill":
                    SetStatus("กินยาแล้ว ร่างกายดูดซับรังสีน้อยลง ~50%", 3f);
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

        void Win()
        {
            finished = true;
            for (int i = 0; i < done.Length; i++) done[i] = true;
            sfx.PlayOneShot(winClip);
            RefreshBoard();
            SetStatus("สำเร็จ! แตะปุ่ม RESET เพื่อเล่นใหม่", 0f);
            if (successPanel == null) return;

            ShowPanel(successColor);

            if (successText != null)
            {
                int m = (int)(elapsed / 60f), s = (int)(elapsed % 60f);
                float dose = radiation != null ? radiation.TotalDose : 0f;
                successText.text =
                    "<size=96><color=#5CFF8A>สำเร็จ / ปลอดภัย</color></size>\n\n" +
                    "คุณฝ่าโซนรังสีรั่วไหลออกมาได้อย่างปลอดภัย\n\n" +
                    $"เวลาที่ใช้: {m:00}:{s:00}     รังสีสะสม: {dose:0.00} µSv\n\n" +
                    "<size=34><color=#BBBBBB>เอามือแตะปุ่ม RESET  หรือกด B  (คีย์บอร์ด: R)  เพื่อเริ่มใหม่</color></size>";
            }
        }

        void ShowPanel(Color background)
        {
            successPanel.SetActive(true);
            var bg = successPanel.GetComponentInChildren<Image>();
            if (bg != null) bg.color = background;
            Transform head = rig.head;
            Vector3 fwd = rig.FlatForward;
            Vector3 pos = head.position + fwd * 0.75f;
            pos.y = head.position.y - 0.15f;
            successPanel.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(fwd, Vector3.up));
        }

        /// <summary>Too much radiation: show a red screen, then restart automatically.</summary>
        void Fail()
        {
            finished = true;
            failed = true;
            restartTimer = failRestartDelay;
            sfx.PlayOneShot(errorClip);
            SetStatus("ได้รับรังสีเกินขีดจำกัด! กำลังเริ่มใหม่...", 0f);
            if (successPanel != null) ShowPanel(failColor);
            UpdateFailText();
        }

        void UpdateFailText()
        {
            if (successText == null) return;
            float dose = radiation != null ? radiation.TotalDose : 0f;
            successText.text =
                "<size=96><color=#FF5A4A>ล้มเหลว: ได้รับรังสีเกิน</color></size>\n\n" +
                $"รังสีสะสม {dose:0.0} µSv  (ขีดจำกัด {doseLimit:0} µSv)\n\n" +
                "อย่าอยู่ใกล้จุดรั่วนาน  ใส่ชุด กินยา และปิดพัดลมก่อนฝ่าออกไป\n\n" +
                $"<size=40><color=#FFD54A>เริ่มใหม่ใน {Mathf.CeilToInt(Mathf.Max(restartTimer, 0f))} วินาที...</color></size>";
        }

        // ---------------------------------------------------------------- update

        void Update()
        {
            if (!finished) elapsed += Time.deltaTime;

            // over the limit -> fail, then restart by itself
            if (!finished && radiation != null && radiation.TotalDose >= doseLimit) Fail();
            if (failed)
            {
                restartTimer -= Time.deltaTime;
                UpdateFailText();
                if (restartTimer <= 0f) { ResetGame(); return; }
            }

            if (!finished && rig != null && exitPoint != null)
            {
                Vector3 a = rig.head.position; a.y = 0f;
                Vector3 b = exitPoint.position; b.y = 0f;
                if (Vector3.Distance(a, b) < exitRadius)
                {
                    if (PrepDone()) Win();
                    else if (statusTimer <= 0f)
                    {
                        SetStatus($"อันตราย! ยังเตรียมตัวไม่ครบ – ทำ Step {FirstUndone() + 1} ก่อนออกจากห้อง", 3f);
                        sfx.PlayOneShot(errorClip);
                    }
                }
            }

            if (statusTimer > 0f)
            {
                statusTimer -= Time.deltaTime;
                if (statusTimer <= 0f && !finished) SetStatus(DefaultStatus, 0f);
            }

            UpdateHighlights();
            UpdateHud();
        }

        void UpdateHighlights()
        {
            if (interactables == null || interactableIds == null) return;
            int current = FirstUndone();
            string currentId = current < steps.Length ? steps[current].id : null;
            for (int i = 0; i < interactables.Length && i < interactableIds.Length; i++)
                if (interactables[i] != null)
                    interactables[i].highlighted = !finished && interactableIds[i] == currentId;
        }

        void UpdateHud()
        {
            if (hudText == null || radiation == null) return;
            string zone = radiation.InDanger
                ? "<color=#FF4040>!! อยู่ในโซนรังสี !!</color>"
                : radiation.CurrentDoseRate > 20f ? "<color=#FFC040>ระวัง: ใกล้จุดรั่ว</color>"
                : "<color=#60FF90>ระดับปลอดภัย</color>";
            hudText.text =
                $"รังสี {radiation.CurrentDoseRate:0.0} µSv/h\n" +
                $"สะสม {radiation.TotalDose:0.0} / {doseLimit:0} µSv\n" +
                $"ชุด: {(IsDone("suit") ? "ใส่แล้ว" : "-")}   ยา: {(IsDone("pill") ? "กินแล้ว" : "-")}\n{zone}";
        }

        void RefreshBoard()
        {
            if (boardSteps == null) return;
            int current = FirstUndone();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < steps.Length; i++)
            {
                if (done[i])
                    sb.Append($"<color=#5CFF8A>Step {i + 1}: {steps[i].title}  (เสร็จแล้ว)</color>\n\n");
                else if (i == current)
                    sb.Append($"<color=#FFD54A>> Step {i + 1}: {steps[i].title}</color>\n<size=34>   {steps[i].howTo}</size>\n\n");
                else
                    sb.Append($"<color=#999999>Step {i + 1}: {steps[i].title}</color>\n\n");
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
