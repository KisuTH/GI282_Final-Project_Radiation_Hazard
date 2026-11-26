using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RadiationShowcase
{
    /// <summary>
    /// Offline "smart assistant" tablet — no internet, no API.
    /// Rule/state based: every message is picked instantly from a local library according to the live scene state
    /// (current step, distance / dose rate at the player, equipment status, accumulated dose).
    ///
    /// 1) Live advice line, refreshed every frame from prioritised rules (danger first, then the next step).
    /// 2) Quick-question buttons (poke with the other hand in VR, keys 1–4 on desktop) answered immediately.
    /// Numeric radiation values are only quoted while the player holds the Geiger counter
    /// (the game rule: radiation is read through the instrument).
    /// The tablet folds away: X (left controller) / Tab toggles it. In VR it pops up floating in front of you and
    /// stays put there; on desktop it sits at the bottom-left of the view while open.
    /// </summary>
    public class RadiationAssistant : MonoBehaviour
    {
        [Header("Scene state")]
        public ShowcaseRig rig;
        public GameFlow flow;
        public RadiationSource radiation;
        public GeigerCounter geiger;
        public Grabbable axe;
        public BreakableGlass glass;
        public Transform exitPoint;

        [Header("UI")]
        public Transform tablet;           // the tablet canvas (follows the left hand / sits bottom-left on desktop)
        public Text liveText;
        public Text answerText;
        [Tooltip("How long a quick-question answer stays on screen (s)")]
        public float answerSeconds = 9f;

        const string Red = "#FF5A4A", Orange = "#FFA040", Yellow = "#FFD54A", Green = "#6CFF8E", Grey = "#AAAAAA";

        bool open;
        bool wasActive;
        [Tooltip("Pop the tablet up as soon as the game starts (and after every restart)")]
        public bool openOnStart = true;
        float popT;
        InputAction toggleAction;
        string lastLive;
        int lastLevel;
        float answerTimer;
        AudioSource sfx;
        AudioClip warnClip, answerClip;

        void Awake()
        {
            sfx = gameObject.AddComponent<AudioSource>();
            sfx.playOnAwake = false;
            sfx.spatialBlend = 0f;
            warnClip = ProceduralAudio.Notes("AssistantWarn", new[] { 1200f, 900f, 1200f }, 0.07f, 0.45f);
            answerClip = ProceduralAudio.Notes("AssistantAnswer", new[] { 1000f, 1300f }, 0.05f, 0.35f);

            toggleAction = new InputAction("ToggleTablet", InputActionType.Button);
            toggleAction.AddBinding("<XRController>{LeftHand}/primaryButton");
            toggleAction.AddBinding("<XRController>{LeftHand}/{PrimaryButton}");
            toggleAction.AddBinding("<Keyboard>/tab");
            if (tablet != null) tablet.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            WorldButton.Pressed += OnButton;
            toggleAction.Enable();
        }

        void OnDisable()
        {
            WorldButton.Pressed -= OnButton;
            toggleAction.Disable();
        }

        void OnDestroy() => toggleAction.Dispose();

        public void Open()
        {
            if (open || tablet == null || rig == null) return;
            open = true;
            popT = 0f;
            tablet.gameObject.SetActive(true);
            if (rig.VRActive)
            {
                // pop up in front of the chest, within reach, then stay put
                Vector3 pos = rig.head.position + rig.FlatForward * 0.45f - Vector3.up * 0.25f;
                tablet.SetPositionAndRotation(pos, Quaternion.LookRotation(pos - rig.head.position, Vector3.up));
            }
            Physics.SyncTransforms();
        }

        public void Close()
        {
            open = false;
            if (tablet != null) tablet.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- input

        void OnButton(WorldButton b)
        {
            if (PauseMenu.Paused || flow == null || flow.IsFinished) return;
            switch (b.id)
            {
                case "ask_next": Answer(AnswerNext()); break;
                case "ask_safe": Answer(AnswerSafe()); break;
                case "ask_gear": Answer(AnswerGear()); break;
                case "ask_body": Answer(AnswerBody()); break;
            }
        }

        void Update()
        {
            bool active = flow != null && !flow.IsFinished && !PauseMenu.Paused;
            if (!active)
            {
                if (open) Close();
                wasActive = false;
                return;
            }

            // the game has just started (main menu closed / restarted): show the tablet right away
            if (!wasActive && openOnStart && flow.Elapsed < 0.5f) Open();
            wasActive = true;

            if (toggleAction.WasPressedThisFrame())
            {
                if (open) Close();
                else Open();
            }

            // desktop: number keys ask the quick questions
            var kb = Keyboard.current;
            if (rig != null && !rig.VRActive && kb != null)
            {
                if (kb.digit1Key.wasPressedThisFrame) Answer(AnswerNext());
                if (kb.digit2Key.wasPressedThisFrame) Answer(AnswerSafe());
                if (kb.digit3Key.wasPressedThisFrame) Answer(AnswerGear());
                if (kb.digit4Key.wasPressedThisFrame) Answer(AnswerBody());
            }

            // live advice (rule engine)
            string live = LiveAdvice(out int level);
            if (liveText != null && live != lastLive) liveText.text = live;
            if (level >= 2 && level > lastLevel) sfx.PlayOneShot(warnClip);   // new / worse warning
            lastLive = live;
            lastLevel = level;

            if (answerTimer > 0f)
            {
                answerTimer -= Time.deltaTime;
                if (answerTimer <= 0f && answerText != null)
                    answerText.text = $"<color={Grey}>แตะปุ่มด้านล่างเพื่อถาม (PC: กด 1–4)  ·  X / Tab = พับเก็บ</color>";
            }
        }

        void LateUpdate()
        {
            if (tablet == null || rig == null || !open) return;

            // little pop-up animation
            popT = Mathf.Min(1f, popT + Time.unscaledDeltaTime / 0.15f);
            float k = 1f - (1f - popT) * (1f - popT);
            tablet.localScale = Vector3.one * (0.00035f * Mathf.Lerp(0.2f, 1f, k));

            if (!rig.VRActive)
            {
                // desktop: bottom-left of the view while open
                Transform head = rig.head;
                tablet.SetPositionAndRotation(
                    head.position + head.forward * 0.8f - head.right * 0.36f - head.up * 0.2f, head.rotation);
            }
        }

        void Answer(string text)
        {
            Open();
            if (answerText != null) answerText.text = text;
            answerTimer = answerSeconds;
            sfx.PlayOneShot(answerClip);
        }

        // ---------------------------------------------------------------- state helpers

        bool GeigerInHand => geiger != null && geiger.IsHeld;
        float BodyRate => radiation != null ? radiation.CurrentDoseRate : 0f;           // Sv/h after protection
        float Dose => radiation != null ? radiation.TotalDose : 0f;                      // Sv
        float Lethal => flow != null ? flow.lethalDose : 4f;
        bool Done(string id) => flow != null && flow.IsStepDone(id);

        float DistanceToExit
        {
            get
            {
                if (exitPoint == null || rig == null) return 99f;
                Vector3 a = rig.head.position; a.y = 0f;
                Vector3 b = exitPoint.position; b.y = 0f;
                return Vector3.Distance(a, b);
            }
        }

        /// <summary>0 safe, 1 caution, 2 danger, 3 lethal — from the dose rate the body gets right here.</summary>
        int Level(float svPerHour)
        {
            if (svPerHour < 0.001f) return 0;   // < 1 mSv/h
            if (svPerHour < 0.1f) return 1;     // < 100 mSv/h
            if (svPerHour < 10f) return 2;      // < 10 Sv/h
            return 3;
        }

        string SecondsTo(float targetSv)
        {
            float perSecond = BodyRate / 3600f;
            if (perSecond <= 0f || Dose >= targetSv) return null;
            float s = (targetSv - Dose) / perSecond;
            if (s > 3600f) return null;
            return s < 60f ? $"{s:0} วินาที" : $"{s / 60f:0.#} นาที";
        }

        // ---------------------------------------------------------------- 1) live advice rules (priority order)

        string LiveAdvice(out int level)
        {
            level = Level(BodyRate);
            bool prepDone = flow != null && flow.AllPrepDone;

            // R1: lethal dose rate right now
            if (level == 3)
            {
                string t = GeigerInHand ? SecondsTo(Lethal) : null;
                return $"<color={Red}><b>!! อันตรายถึงชีวิต !!</b></color> ถอยออกจากช่องลมทันที" +
                       (t != null ? $"\nอยู่ตรงนี้ต่ออีก ~{t} = เสียชีวิต" : "");
            }
            // R2: body already badly irradiated
            if (Dose >= Lethal * 0.5f)
            {
                level = Mathf.Max(level, 2);
                return $"<color={Red}><b>ร่างกายรับรังสีสูงมาก</b></color> ตาเริ่มมืด = ใกล้ขีดอันตราย\nรีบไปทางออกให้เร็วที่สุด อย่าแวะใกล้ช่องลม";
            }
            // R3: in the red zone before being ready
            if (radiation != null && radiation.InDanger && !prepDone)
            {
                level = Mathf.Max(level, 2);
                return $"<color={Orange}><b>คุณยังเตรียมตัวไม่ครบ</b></color> รังสีตามมาทันแล้ว รีบเดินหนีไปข้างหน้า\n{NextStepShort()}";
            }
            // R4: everything ready -> go
            if (prepDone)
            {
                return DistanceToExit < 3f
                    ? $"<color={Green}><b>พร้อมแล้ว!</b></color> ออกทางประตู EXIT เลย (กดสติ๊กซ้าย = วิ่ง)"
                    : $"<color={Green}><b>เตรียมตัวครบแล้ว</b></color> ไปที่ประตู EXIT สุดห้อง เดินซิกแซกอ้อมกองลังตามเส้นเขียว อย่าหยุดยืน";
            }
            // R5: picked the Geiger up earlier but put it down
            if (Done("geiger") && geiger != null && !geiger.IsHeld && level >= 1)
                return $"<color={Yellow}>อย่าลืมถือ Geiger Counter</color> ไว้ดูค่ารังสีตลอด\n{NextStepShort()}";
            // R6: caution zone
            if (level >= 1)
                return $"<color={Yellow}>รังสีเริ่มสูงบริเวณนี้</color> อย่าอยู่นาน\n{NextStepShort()}";
            // R7: default — what to do next
            return NextStepShort();
        }

        string NextStepShort()
        {
            switch (flow != null ? flow.CurrentStepId : null)
            {
                case "geiger":
                    if (glass != null && !glass.Broken)
                        return axe != null && axe.IsHeld
                            ? "<b>ถัดไป:</b> เหวี่ยงขวานแรงๆ ใส่กระจกตู้ฉุกเฉิน"
                            : "<b>ถัดไป:</b> หยิบขวานดับเพลิงข้างตู้ฉุกเฉิน (ผนังขวา ข้างจุดเริ่ม)";
                    return "<b>ถัดไป:</b> หยิบ Geiger Counter ในตู้แล้วถือไว้";
                case "suit": return "<b>ถัดไป:</b> ใส่ชุดกันรังสี — ราวแขวนหลังกองลังแถวแรก จับแล้วดึงแนบตัว";
                case "fan": return "<b>ถัดไป:</b> ปิดพัดลม — โยกคันโยกแดงบนแผงสีฟ้าข้างประตู EXIT ลงสุด";
                case "pill": return "<b>ถัดไป:</b> กินยา KI — ขวดสีส้มบนโต๊ะหลังกองลังแถวที่ 2 ยกขึ้นที่ปาก";
                default: return "<b>ถัดไป:</b> ไปที่ประตู EXIT สุดห้อง (อ้อมกองลังตามเส้นเขียว)";
            }
        }

        // ---------------------------------------------------------------- 2) quick questions

        string AnswerNext()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append($"<color={Yellow}><b>ต้องทำอะไรต่อ?</b></color>\n").Append(NextStepShort()).Append('\n');
            string[] ids = { "geiger", "suit", "fan", "pill" };
            string[] names = { "Geiger", "ชุด", "พัดลม", "ยา KI" };
            sb.Append("เหลือ: ");
            bool any = false;
            for (int i = 0; i < ids.Length; i++)
                if (!Done(ids[i])) { sb.Append(any ? ", " : "").Append(names[i]); any = true; }
            sb.Append(any ? " แล้วค่อยไป EXIT" : "ไม่มี — ไป EXIT ได้เลย");
            return sb.ToString();
        }

        string AnswerSafe()
        {
            int level = Level(BodyRate);
            string[] verdict =
            {
                $"<color={Green}><b>ปลอดภัย</b></color> รังสีต่ำมาก อยู่ได้",
                $"<color={Yellow}><b>พอได้ แต่อย่าอยู่นาน</b></color> รังสีสูงกว่าปกติ",
                $"<color={Orange}><b>อันตราย</b></color> ร่างกายสะสมรังสีเร็ว ออกจากจุดนี้",
                $"<color={Red}><b>อันตรายถึงชีวิต</b></color> ถอยทันที!",
            };
            string s = $"<color={Yellow}><b>จุดนี้ปลอดภัยไหม?</b></color>\n{verdict[level]}";
            if (GeigerInHand)
            {
                s += $"\nเครื่องวัดอ่านได้ {RadiationEffects.Rate(geiger.MeasuredRate)} (ก่อนหักการป้องกัน)";
                string t = SecondsTo(1f);
                if (t != null) s += $"\nถึง 1 Sv (เริ่มป่วยจากรังสี) ใน ~{t}";
            }
            else s += $"\n<color={Grey}>ถือ Geiger Counter เพื่อดูค่าที่แน่นอน</color>";
            return s;
        }

        string AnswerGear()
        {
            string Mark(bool ok) => ok ? $"<color={Green}>[พร้อม]</color>" : $"<color={Red}>[ยัง]</color>";
            float p = radiation != null ? radiation.protection : 1f;
            return $"<color={Yellow}><b>อุปกรณ์ของฉัน?</b></color>\n" +
                   $"{Mark(GeigerInHand)} Geiger {(GeigerInHand ? "ถืออยู่" : Done("geiger") ? "วางไว้ — หยิบกลับมาถือ" : "ยังอยู่ในตู้")}   " +
                   $"{Mark(Done("suit"))} ชุดกันรังสี\n" +
                   $"{Mark(Done("pill"))} ยา KI   {Mark(Done("fan"))} ปิดพัดลมแล้ว\n" +
                   $"การป้องกันตอนนี้: รับรังสีเพียง {p * 100f:0}% ของปกติ";
        }

        string AnswerBody()
        {
            string s = $"<color={Yellow}><b>ร่างกายฉันเป็นไง?</b></color>\n";
            if (GeigerInHand) s += $"DOSE สะสม {RadiationEffects.Dose(Dose)} — ";
            s += RadiationEffects.Describe(Dose).Replace("\n", " · ");
            if (!GeigerInHand) s += $"\n<color={Grey}>ถือ Geiger เพื่อดูตัวเลขที่แน่นอน</color>";
            return s;
        }
    }
}
