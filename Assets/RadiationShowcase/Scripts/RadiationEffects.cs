using UnityEngine;

namespace RadiationShowcase
{
    /// <summary>
    /// Real-world units and health effects of an acute whole-body dose (gamma, so 1 Gy ≈ 1 Sv).
    /// Thresholds follow CDC "Acute Radiation Syndrome" guidance for clinicians:
    ///  - mild symptoms possible from ~0.3 Gy, full ARS above ~0.7 Gy
    ///  - some deaths possible from ~1.2 Gy, LD50/60 about 2.5–5 Gy without treatment
    ///  - GI syndrome above ~10 Gy (symptoms from 6 Gy), CNS/cardiovascular above ~50 Gy (from 20 Gy)
    /// </summary>
    public static class RadiationEffects
    {
        /// <summary>Formats a dose in sievert with a readable unit (µSv / mSv / Sv).</summary>
        public static string Dose(float sv)
        {
            if (sv < 0.001f) return $"{sv * 1e6f:0} µSv";
            if (sv < 1f) return $"{sv * 1e3f:0.#} mSv";
            return $"{sv:0.00} Sv";
        }

        /// <summary>Formats a dose rate given in Sv per hour.</summary>
        public static string Rate(float svPerHour)
        {
            if (svPerHour < 0.001f) return $"{svPerHour * 1e6f:0.#} µSv/h";
            if (svPerHour < 1f) return $"{svPerHour * 1e3f:0.#} mSv/h";
            if (svPerHour < 1000f) return $"{svPerHour:0.#} Sv/h";
            return $"{svPerHour / 1000f:0.#} kSv/h";
        }

        public static Color LevelColor(float sv)
        {
            if (sv < 0.1f) return new Color(0.4f, 1f, 0.55f);
            if (sv < 0.7f) return new Color(1f, 0.85f, 0.3f);
            if (sv < 2f) return new Color(1f, 0.55f, 0.2f);
            return new Color(1f, 0.25f, 0.2f);
        }

        /// <summary>Short title + what happens to the body at this whole-body dose.</summary>
        public static string Describe(float sv)
        {
            if (sv < 0.01f)
                return "ไม่มีผลต่อร่างกาย\n" +
                       "ใกล้เคียงรังสีที่คนเราได้รับจากธรรมชาติภายในไม่กี่ปี หรือการตรวจ CT scan หนึ่งครั้ง";
            if (sv < 0.1f)
                return "ไม่มีอาการเฉียบพลัน\n" +
                       "ร่างกายรู้สึกปกติ แต่ความเสี่ยงเป็นมะเร็งในระยะยาวเพิ่มขึ้นเล็กน้อย";
            if (sv < 0.3f)
                return "ยังไม่มีอาการ แต่ร่างกายเริ่มได้รับผลกระทบ\n" +
                       "อาจมีการเปลี่ยนแปลงของเม็ดเลือดชั่วคราว และความเสี่ยงมะเร็งระยะยาวเพิ่มขึ้น";
            if (sv < 0.7f)
                return "อาจเริ่มมีอาการเล็กน้อย\n" +
                       "บางคนคลื่นไส้ อ่อนเพลีย เม็ดเลือดขาวลดลงชั่วคราว ส่วนใหญ่ฟื้นตัวได้เอง";
            if (sv < 2f)
                return "กลุ่มอาการป่วยจากรังสีเฉียบพลัน (ARS) ระดับไขกระดูก\n" +
                       "คลื่นไส้ อาเจียนภายในไม่กี่ชั่วโมง อ่อนเพลีย เม็ดเลือดขาวลด ติดเชื้อง่าย\n" +
                       "บางคนอาจเสียชีวิตได้ตั้งแต่ราว 1.2 Sv หากไม่ได้รับการรักษา";
            if (sv < 3.5f)
                return "ARS ระดับปานกลางถึงรุนแรง\n" +
                       "มีไข้ ติดเชื้อ เลือดออกง่าย ผมร่วงภายใน 2–3 สัปดาห์ ต้องได้รับการรักษาในโรงพยาบาล";
            if (sv < 6f)
                return "อันตรายถึงชีวิต (ระดับ LD50)\n" +
                       "ไขกระดูกถูกทำลาย ถ้าไม่ได้รับการรักษา ประมาณครึ่งหนึ่งของผู้ได้รับรังสีระดับนี้เสียชีวิตภายใน 60 วัน";
            if (sv < 10f)
                return "อันตรายถึงชีวิตอย่างรุนแรง\n" +
                       "เริ่มมีอาการทางลำไส้ ท้องเสีย อาเจียน เวียนหัว ความดันต่ำ แม้รักษาอย่างดีก็มีโอกาสรอดต่ำ";
            if (sv < 20f)
                return "กลุ่มอาการทางเดินอาหาร (GI syndrome)\n" +
                       "ลำไส้ถูกทำลายถาวร เสียชีวิตภายใน 1–2 สัปดาห์";
            return "กลุ่มอาการระบบประสาท/หัวใจหลอดเลือด\n" +
                   "สับสน หมดสติ ผิวหนังแสบไหม้ เสียชีวิตภายในไม่กี่ชั่วโมงถึงไม่กี่วัน";
        }

        /// <summary>Dose table for the handbook.</summary>
        public const string HandbookTable =
            "ต่ำกว่า 10 mSv  ไม่มีผล (เท่ารังสีธรรมชาติไม่กี่ปี / CT 1 ครั้ง)\n" +
            "10–100 mSv  ไม่มีอาการ ความเสี่ยงมะเร็งระยะยาวเพิ่มเล็กน้อย\n" +
            "0.3–0.7 Sv  อาจเริ่มคลื่นไส้ อ่อนเพลีย\n" +
            "0.7–2 Sv  ป่วยจากรังสีเฉียบพลัน (ARS): อาเจียน เม็ดเลือดขาวลด\n" +
            "2–3.5 Sv  ไข้ ติดเชื้อ เลือดออก ผมร่วง\n" +
            "3.5–6 Sv  อันตรายถึงชีวิต (LD50) ← ในเกมนี้เกิน 4 Sv = เสียชีวิต\n" +
            "6–10 Sv  อาการทางลำไส้ โอกาสรอดต่ำมาก\n" +
            "มากกว่า 10–20 Sv  เสียชีวิตภายในไม่กี่วันถึง 2 สัปดาห์";
    }
}
