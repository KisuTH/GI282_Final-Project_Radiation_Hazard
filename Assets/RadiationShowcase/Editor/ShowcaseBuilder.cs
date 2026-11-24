using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using RadiationShowcase;

/// <summary>
/// Menu: Tools > Radiation Showcase > Build VR Scene
/// Builds Assets/RadiationShowcase/Scenes/RadiationShowcase.unity from scratch (room, vent, guide board, rig, UI).
/// Run it again any time to rebuild the scene.
/// </summary>
public static class ShowcaseBuilder
{
    const string Root = "Assets/RadiationShowcase";
    const string GenDir = Root + "/Generated";
    const string SceneDir = Root + "/Scenes";
    const string ScenePath = SceneDir + "/RadiationShowcase.unity";

    static List<Text> texts;
    static Font editorFont;

    // materials
    static Material mFloor, mWall, mCeiling, mMetal, mDark, mYellow, mOrangeBtn, mBlue, mRed, mGreenPath,
        mDanger, mVentGlow, mExit, mLamp, mBoard, mHand, mParticle, mSuit, mVisor, mPill, mCap, mLine, mReticle;

    [MenuItem("Tools/Radiation Showcase/Build VR Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EnsureFolder("Assets", "RadiationShowcase");
        EnsureFolder(Root, "Generated");
        EnsureFolder(Root, "Scenes");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        texts = new List<Text>();
        editorFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        CreateMaterials();

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.28f, 0.29f, 0.32f);
        RenderSettings.skybox = null;

        // ------------------------------------------------------------ room
        var env = new GameObject("Environment").transform;
        Box("Floor", env, new Vector3(0, -0.05f, 0), new Vector3(8, 0.1f, 6), mFloor).AddComponent<TeleportArea>();
        Box("Ceiling", env, new Vector3(0, 3.05f, 0), new Vector3(8, 0.1f, 6), mCeiling);
        Box("Wall_North", env, new Vector3(0, 1.5f, 3.1f), new Vector3(8.4f, 3, 0.2f), mWall);
        Box("Wall_South", env, new Vector3(0, 1.5f, -3.1f), new Vector3(8.4f, 3, 0.2f), mWall);
        Box("Wall_West", env, new Vector3(-4.1f, 1.5f, 0), new Vector3(0.2f, 3, 6), mWall);
        Box("Wall_East", env, new Vector3(4.1f, 1.5f, 0), new Vector3(0.2f, 3, 6), mWall);
        Box("Stripe_N", env, new Vector3(0, 0.1f, 2.99f), new Vector3(8, 0.2f, 0.02f), mYellow, false);
        Box("Stripe_S", env, new Vector3(0, 0.1f, -2.99f), new Vector3(8, 0.2f, 0.02f), mYellow, false);

        foreach (var x in new[] { -2f, 2f })
        {
            Box("Lamp", env, new Vector3(x, 2.98f, 0), new Vector3(1.2f, 0.04f, 0.3f), mLamp, false);
            var l = new GameObject("CeilingLight").AddComponent<Light>();
            l.transform.SetParent(env, false);
            l.transform.localPosition = new Vector3(x, 2.7f, 0);
            l.type = LightType.Point; l.range = 8f; l.intensity = 1.6f;
            l.color = new Color(1f, 0.95f, 0.85f); l.shadows = LightShadows.None;
        }

        // ------------------------------------------------------------ vent + radiation (east wall, right next to the exit door)
        // the group is turned 180° so its local +X points into the room
        var radGo = new GameObject("RadiationSource (Air Vent)");
        radGo.transform.SetPositionAndRotation(new Vector3(3.9f, 0, -0.4f), Quaternion.Euler(0, 180, 0));
        var rad = radGo.AddComponent<RadiationSource>();
        var vent = radGo.transform;

        Box("VentFrame", vent, new Vector3(-0.04f, 2.3f, 0), new Vector3(0.12f, 0.9f, 1.3f), mMetal);
        Box("VentGlow", vent, new Vector3(0.025f, 2.3f, 0), new Vector3(0.01f, 0.7f, 1.1f), mVentGlow, false);
        var blades = new GameObject("FanBlades").transform;
        blades.SetParent(vent, false);
        blades.localPosition = new Vector3(0.04f, 2.3f, 0);
        for (int i = 0; i < 4; i++)
        {
            var b = Box("Blade", blades, Vector3.zero, new Vector3(0.015f, 0.62f, 0.09f), mDark, false);
            b.transform.localRotation = Quaternion.Euler(i * 45f, 0, 0);
        }
        for (int i = 0; i < 6; i++)
            Box("Slat", vent, new Vector3(0.08f, 2.0f + i * 0.12f, 0), new Vector3(0.03f, 0.03f, 1.2f), mMetal, false);

        Label("HazardSign", vent, new Vector3(0.1f, 1.55f, 0), Quaternion.Euler(0, -90, 0),
            "!! อันตราย !!\nรังสีรั่วไหลจากช่องลม", 64, new Color(1f, 0.3f, 0.2f), new Vector2(1100, 300), 0.001f,
            new Color(0, 0, 0, 0.75f));

        var glow = new GameObject("VentLight").AddComponent<Light>();
        glow.transform.SetParent(vent, false);
        glow.transform.localPosition = new Vector3(0.5f, 2.2f, 0);
        glow.type = LightType.Point; glow.range = 6f; glow.intensity = 3f;
        glow.color = new Color(0.4f, 1f, 0.3f); glow.shadows = LightShadows.None;

        var danger = Box("DangerZone", vent, new Vector3(0, 0.004f, 0), new Vector3(5.2f, 0.002f, 5.2f), mDanger, false, PrimitiveType.Cylinder);
        var ps = CreateParticles(vent);

        rad.dangerRadius = 2.6f;
        rad.beepRange = 7f;
        rad.particles = ps;
        rad.glowLight = glow;
        rad.fanBlades = blades;
        rad.dangerFloor = danger.GetComponent<Renderer>();

        // ------------------------------------------------------------ Step 1: radiation suit on a rack (west wall)
        var rack = new GameObject("Step1_SuitRack").transform;
        rack.SetPositionAndRotation(new Vector3(-3.75f, 0, -1.0f), Quaternion.Euler(0, 90, 0)); // local +Z = into the room
        Box("Post_L", rack, new Vector3(-0.5f, 1.0f, -0.1f), new Vector3(0.05f, 2.0f, 0.05f), mMetal);
        Box("Post_R", rack, new Vector3(0.5f, 1.0f, -0.1f), new Vector3(0.05f, 2.0f, 0.05f), mMetal);
        Box("Bar", rack, new Vector3(0, 2.0f, -0.1f), new Vector3(1.05f, 0.04f, 0.04f), mMetal);
        Box("Hanger", rack, new Vector3(0, 1.96f, 0.05f), new Vector3(0.02f, 0.1f, 0.02f), mDark, false);
        Label("RackSign", rack, new Vector3(0, 2.3f, 0), Quaternion.Euler(0, 180, 0), // face the player (who looks toward the wall)
            "STEP 1\nชุดป้องกันรังสี", 60, Color.black, new Vector2(900, 240), 0.001f, new Color(0.95f, 0.75f, 0.1f, 0.95f));

        var suitGo = new GameObject("RadiationSuit (grab + pull to chest)");
        suitGo.transform.SetPositionAndRotation(rack.TransformPoint(0, 1.3f, 0.05f), rack.rotation);
        var st = suitGo.transform;
        Box("Torso", st, Vector3.zero, new Vector3(0.45f, 0.6f, 0.18f), mSuit, false);
        Box("Hood", st, new Vector3(0, 0.42f, 0), new Vector3(0.28f, 0.3f, 0.28f), mSuit, false, PrimitiveType.Sphere);
        Box("Visor", st, new Vector3(0, 0.43f, 0.13f), new Vector3(0.2f, 0.12f, 0.03f), mVisor, false);
        Box("Arm_L", st, new Vector3(-0.3f, -0.02f, 0), new Vector3(0.12f, 0.55f, 0.12f), mSuit, false);
        Box("Arm_R", st, new Vector3(0.3f, -0.02f, 0), new Vector3(0.12f, 0.55f, 0.12f), mSuit, false);
        Box("Leg_L", st, new Vector3(-0.11f, -0.6f, 0), new Vector3(0.18f, 0.6f, 0.16f), mSuit, false);
        Box("Leg_R", st, new Vector3(0.11f, -0.6f, 0), new Vector3(0.18f, 0.6f, 0.16f), mSuit, false);
        Box("Badge", st, new Vector3(0.1f, 0.15f, 0.095f), new Vector3(0.08f, 0.08f, 0.01f), mDanger, false);
        var suitCol = suitGo.AddComponent<BoxCollider>();
        suitCol.center = new Vector3(0, -0.15f, 0);
        suitCol.size = new Vector3(0.72f, 1.5f, 0.3f);
        var suitRb = suitGo.AddComponent<Rigidbody>();
        suitRb.mass = 3f;
        suitRb.interpolation = RigidbodyInterpolation.Interpolate;
        suitRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        suitRb.isKinematic = true;
        var suit = suitGo.AddComponent<WearableItem>();
        suit.startKinematic = true;
        suit.actionId = "suit";

        // ------------------------------------------------------------ Step 2: fan lever (north wall)
        var sw = new GameObject("Step2_FanLever").transform;
        sw.position = new Vector3(0.6f, 0, 2.95f);
        Box("SwitchPanel", sw, new Vector3(0, 1.3f, 0), new Vector3(0.6f, 0.8f, 0.08f), mBlue);
        Box("Slot", sw, new Vector3(0, 1.15f, -0.045f), new Vector3(0.06f, 0.5f, 0.01f), mDark, false);
        var pivot = new GameObject("LeverPivot").transform;
        pivot.SetParent(sw, false);
        pivot.localPosition = new Vector3(0, 1.2f, -0.07f);
        pivot.localRotation = Quaternion.Euler(0, 180, 0); // local +Z faces the player: the lever swings toward you and down
        Box("PivotHub", pivot, Vector3.zero, new Vector3(0.09f, 0.09f, 0.09f), mDark, false, PrimitiveType.Sphere);
        var arm = Box("LeverArm", pivot, new Vector3(0, 0.17f, 0), new Vector3(0.045f, 0.32f, 0.045f), mRed, false);
        var knob = Box("LeverKnob (grab)", pivot, new Vector3(0, 0.34f, 0), new Vector3(0.1f, 0.1f, 0.1f), mRed, true, PrimitiveType.Sphere);
        var leverHandle = knob.AddComponent<LeverHandle>();
        leverHandle.pivot = pivot;
        leverHandle.actionId = "fan";
        leverHandle.glowRenderers = new[] { knob.GetComponent<Renderer>(), arm.GetComponent<Renderer>() };
        var fanLabel = Label("FanStatus", sw, new Vector3(0.45f, 1.3f, -0.05f), Quaternion.identity,
            "พัดลม\nON", 60, Color.white, new Vector2(260, 200), 0.001f, new Color(0, 0, 0, 0.8f));
        Label("LeverSign", sw, new Vector3(0, 1.98f, -0.05f), Quaternion.identity,
            "STEP 2\nจับคันโยกแล้วโยกลง = ปิดพัดลม", 52, Color.white, new Vector2(1000, 240), 0.001f, new Color(0.05f, 0.15f, 0.35f, 0.9f));

        // ------------------------------------------------------------ Step 3: radiation blocker pills on a table (south wall)
        var table = new GameObject("Step3_PillTable").transform;
        table.position = new Vector3(0.8f, 0, -2.45f);
        Box("TableTop", table, new Vector3(0, 0.8f, 0), new Vector3(0.9f, 0.05f, 0.5f), mMetal);
        foreach (var lx in new[] { -0.4f, 0.4f })
            foreach (var lz in new[] { -0.2f, 0.2f })
                Box("Leg", table, new Vector3(lx, 0.39f, lz), new Vector3(0.04f, 0.78f, 0.04f), mDark);
        Label("PillSign", table, new Vector3(0, 1.25f, -0.2f), Quaternion.Euler(0, 180, 0),
            "STEP 3\nยา Radiation Blocker\nPotassium Iodide (KI)\nหยิบขวดแล้วยกขึ้นที่ปาก", 46, Color.white, new Vector2(900, 360), 0.001f,
            new Color(0.4f, 0.15f, 0.02f, 0.9f));

        var pillGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillGo.name = "PillBottle (grab + bring to mouth)";
        pillGo.transform.position = new Vector3(0.8f, 0.886f, -2.35f);
        pillGo.transform.localScale = new Vector3(0.06f, 0.06f, 0.06f);
        pillGo.GetComponent<Renderer>().sharedMaterial = mPill;
        var cap = Box("Cap", pillGo.transform, new Vector3(0, 1.15f, 0), new Vector3(1.05f, 0.2f, 1.05f), mCap, false, PrimitiveType.Cylinder);
        var pillRb = pillGo.AddComponent<Rigidbody>();
        pillRb.mass = 0.2f;
        pillRb.interpolation = RigidbodyInterpolation.Interpolate;
        pillRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        var pill = pillGo.AddComponent<ConsumableItem>();
        pill.actionId = "pill";
        pill.glowRenderers = new[] { pillGo.GetComponent<Renderer>(), cap.GetComponent<Renderer>() };

        // ------------------------------------------------------------ Exit (east wall, inside the leak zone)
        var exitGrp = new GameObject("Exit").transform;
        exitGrp.position = new Vector3(3.95f, 0, 1.8f);
        Box("Door", exitGrp, new Vector3(0, 1.05f, 0), new Vector3(0.06f, 2.1f, 1.1f), mExit);
        Box("Frame_L", exitGrp, new Vector3(-0.02f, 1.1f, -0.6f), new Vector3(0.1f, 2.2f, 0.1f), mMetal);
        Box("Frame_R", exitGrp, new Vector3(-0.02f, 1.1f, 0.6f), new Vector3(0.1f, 2.2f, 0.1f), mMetal);
        Box("Frame_T", exitGrp, new Vector3(-0.02f, 2.2f, 0), new Vector3(0.1f, 0.1f, 1.3f), mMetal);
        Label("ExitSign", exitGrp, new Vector3(-0.08f, 2.5f, 0), Quaternion.Euler(0, 90, 0),
            "EXIT  /  ทางออก", 80, new Color(0.35f, 1f, 0.5f), new Vector2(1100, 200), 0.001f, new Color(0, 0.1f, 0.03f, 0.9f));
        Box("ExitPad", env, new Vector3(3.35f, 0.008f, 1.8f), new Vector3(1.2f, 0.01f, 1.4f), mGreenPath, false);
        var exitPoint = new GameObject("ExitPoint").transform;
        exitPoint.position = new Vector3(3.3f, 0, 1.8f);
        var exitLight = new GameObject("ExitLight").AddComponent<Light>();
        exitLight.transform.position = new Vector3(3.5f, 2.6f, 1.8f);
        exitLight.type = LightType.Point; exitLight.range = 4f; exitLight.intensity = 1.5f;
        exitLight.color = new Color(0.3f, 1f, 0.45f); exitLight.shadows = LightShadows.None;

        // ------------------------------------------------------------ guide path (green): suit -> lever -> pills -> through the leak -> exit
        var path = new GameObject("GuidePath").transform;
        var pts = new[]
        {
            new Vector3(-2.4f, 0, -0.6f), new Vector3(-3.0f, 0, -1.0f), new Vector3(0.6f, 0, 2.3f),
            new Vector3(0.8f, 0, -1.9f), new Vector3(3.3f, 0, 1.8f)
        };
        for (int i = 0; i < pts.Length - 1; i++) Strip(path, pts[i], pts[i + 1], 0.12f, mGreenPath);

        // ------------------------------------------------------------ start point
        var start = new GameObject("StartPoint").transform;
        start.position = new Vector3(-2.4f, 0, -0.6f);
        start.rotation = Quaternion.identity;

        // ------------------------------------------------------------ guide board (north wall, in front of the start)
        var boardGrp = new GameObject("GuideBoard").transform;
        boardGrp.position = new Vector3(-2.3f, 0, 2.98f);
        Box("BoardBack", boardGrp, new Vector3(0, 1.7f, 0), new Vector3(2.0f, 1.4f, 0.04f), mBoard);
        var boardCanvas = MakeCanvas("BoardCanvas", boardGrp, new Vector3(0, 1.7f, -0.03f), Quaternion.identity,
            new Vector2(1900, 1300), 0.001f, null);
        AddText(boardCanvas, "Title", new Vector2(0.03f, 0.88f), new Vector2(0.97f, 0.98f), 70,
            new Color(1f, 0.85f, 0.25f), TextAnchor.MiddleLeft).text = "คู่มือเอาตัวรอด: รังสีรั่วไหลที่ประตูทางออก";
        var stepsText = AddText(boardCanvas, "Steps", new Vector2(0.04f, 0.30f), new Vector2(0.96f, 0.87f), 42,
            Color.white, TextAnchor.UpperLeft);
        stepsText.text = "Step 1 ...";
        var statusText = AddText(boardCanvas, "Status", new Vector2(0.04f, 0.18f), new Vector2(0.96f, 0.29f), 38,
            new Color(1f, 0.6f, 0.3f), TextAnchor.MiddleLeft);
        AddText(boardCanvas, "Controls", new Vector2(0.04f, 0.01f), new Vector2(0.74f, 0.17f), 26,
            new Color(0.75f, 0.75f, 0.75f), TextAnchor.MiddleLeft).text =
            "VR (แบบ H3VR): สติ๊กซ้าย = เดิน, กดสติ๊กซ้าย = เปิด/ปิดวิ่ง | สติ๊กขวาซ้าย-ขวา = หมุน 45° | สติ๊กขวาดันขึ้น = เล็งวาร์ป ปล่อย = วาร์ป\n" +
            "Grip ค้าง = จับ/ถือ | เอามือจิ้มปุ่ม = กด | Y = เปิด/ปิดคู่มือ | B = รีเซ็ต\n" +
            "PC: คลิกเพื่อล็อกเมาส์ | WASD, Shift = เปิด/ปิดวิ่ง | คลิกขวาค้าง = เล็งวาร์ป | คลิกซ้ายค้าง = จับ | F ค้าง = เอาเข้าตัว/ปาก | H = คู่มือ | R = รีเซ็ต";
        var boardResetGo = Box("Button_Reset", boardGrp, new Vector3(0.75f, 1.12f, -0.05f), new Vector3(0.32f, 0.14f, 0.06f), mRed);
        var boardReset = boardResetGo.AddComponent<WorldButton>();
        boardReset.id = "reset";
        boardReset.emissionColor = new Color(1f, 0.2f, 0.2f);
        Label("ResetLabel", boardGrp, new Vector3(0.75f, 1.12f, -0.085f), Quaternion.identity,
            "RESET", 70, Color.white, new Vector2(320, 140), 0.001f);

        // ------------------------------------------------------------ success panel (appears ~0.75 m in front of the player)
        var successCanvas = MakeCanvas("SuccessPanel", null, new Vector3(0, 1.5f, 0), Quaternion.identity,
            new Vector2(1200, 760), 0.0007f, new Color(0.02f, 0.12f, 0.06f, 0.92f));
        var successText = AddText(successCanvas, "SuccessText", new Vector2(0.04f, 0.28f), new Vector2(0.96f, 0.96f), 46,
            Color.white, TextAnchor.MiddleCenter);
        successText.text = "สำเร็จ / ปลอดภัย";
        var successResetGo = Box("Button_Reset", successCanvas, new Vector3(0, -270, -50), new Vector3(360, 130, 70), mRed);
        var successReset = successResetGo.AddComponent<WorldButton>();
        successReset.id = "reset";
        successReset.emissionColor = new Color(1f, 0.2f, 0.2f);
        Label("ResetLabel", successCanvas, new Vector3(0, -270, -90), Quaternion.identity,
            "RESET", 80, Color.white, new Vector2(360, 130), 1f);
        successCanvas.gameObject.SetActive(false);

        // ------------------------------------------------------------ handbook (Y button)
        var handbook = BuildHandbook();

        // ------------------------------------------------------------ player rig
        var interactables = new Grabbable[] { suit, leverHandle, pill };
        var rig = BuildRig(start, interactables, out Text hudText);
        rad.listener = rig.head;
        rig.handbook = handbook;
        BuildGeigerCounter(rig, rad);

        // ------------------------------------------------------------ post processing
        BuildVolume();

        // ------------------------------------------------------------ game flow
        var flow = new GameObject("GameFlow").AddComponent<GameFlow>();
        flow.rig = rig;
        flow.radiation = rad;
        flow.startPoint = start;
        flow.exitPoint = exitPoint;
        flow.interactables = interactables;
        flow.interactableIds = new[] { "suit", "fan", "pill" };
        flow.fanStatusLabel = fanLabel;
        flow.boardSteps = stepsText;
        flow.boardStatus = statusText;
        flow.hudText = hudText;
        flow.successPanel = successCanvas.gameObject;
        flow.successText = successText;
        flow.allTexts = texts.ToArray();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log($"[Radiation Showcase] Scene built: {ScenePath}");
    }

    // ================================================================= handbook

    static GameObject BuildHandbook()
    {
        // 1300 x 1550 px at 0.0007 m/px = about 0.9 x 1.1 m, floating ~0.9 m in front of the player
        var panel = MakeCanvas("Handbook (Y)", null, new Vector3(0, 1.5f, 0), Quaternion.identity,
            new Vector2(1300, 1550), 0.0007f, new Color(0.04f, 0.05f, 0.07f, 0.95f));

        AddText(panel, "Title", new Vector2(0.04f, 0.93f), new Vector2(0.96f, 0.99f), 54,
            new Color(1f, 0.85f, 0.25f), TextAnchor.MiddleLeft).text =
            "คู่มือป้องกันรังสี  <size=30><color=#AAAAAA>(กด Y อีกครั้งเพื่อปิด)</color></size>";

        const string H = "<color=#FFD54A>";
        const string G = "<color=#5CFF8A>";
        const string R = "<color=#FF8A6A>";
        const string E = "</color>";

        AddText(panel, "Body", new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.92f), 29,
            Color.white, TextAnchor.UpperLeft).text =
            $"{H}1) ชุดป้องกันรังสี{E}   {G}ในเกม: ลดรังสีที่ได้รับ 60%{E}\n" +
            "ทำงานยังไง: กันฝุ่นและอนุภาคกัมมันตรังสีไม่ให้ติดผิวหนังและเสื้อผ้า และกันรังสีแอลฟากับเบตาได้บางส่วน\n" +
            $"{R}ความจริง:{E} ชุดกันรังสีแกมมาได้น้อยมาก ประโยชน์หลักคือกันการปนเปื้อน ถอดชุดทิ้งก็เอาฝุ่นรังสีออกไปด้วย\n\n" +

            $"{H}2) ปิดพัดลมระบายอากาศ{E}   {G}ในเกม: ลดการรั่วไหล 70% (เหลือ 30%){E}\n" +
            "ทำงานยังไง: พัดลมเป่าฝุ่นกัมมันตรังสีจากช่องลมให้ฟุ้งไปทั่วห้อง ปิดแล้วอนุภาคออกมาน้อยลงและไม่กระจาย\n" +
            $"{R}ความจริง:{E} ตรงกับหลักการหลบภัยในอาคาร (Shelter in place) ที่ให้ปิดพัดลม แอร์ และระบบระบายอากาศทั้งหมด\n\n" +

            $"{H}3) ยา Potassium Iodide (KI){E}   {G}ในเกม: ลดรังสีที่ได้รับ 50%{E}\n" +
            "ทำงานยังไง: เติมไอโอดีนปกติให้ต่อมไทรอยด์จนเต็ม ไทรอยด์จึงไม่ดูดไอโอดีนกัมมันตรังสี (I-131) เข้าไปสะสม\n" +
            $"{R}ความจริง:{E} ป้องกันเฉพาะต่อมไทรอยด์จาก I-131 เท่านั้น ไม่กันรังสีจากภายนอกหรือสารกัมมันตรังสีอื่น " +
            "ได้ผลดีที่สุดเมื่อกินก่อนหรือไม่นานหลังสัมผัส และควรกินตามคำแนะนำของเจ้าหน้าที่เท่านั้น\n\n" +

            $"{H}รวมกัน{E}: ชุด + ยา = ได้รับรังสีเหลือ 20% (0.4 x 0.5)  และการปิดพัดลมยังลดความแรงของจุดรั่วลงอีก 70%\n\n" +

            $"{H}4) หลัก เวลา - ระยะทาง - ที่กำบัง{E}\n" +
            "• เวลา: อยู่ในโซนนานเท่าไหร่ ก็ได้รับรังสีมากเท่านั้น → รีบผ่านวงแดง (กดสติ๊กซ้ายเพื่อวิ่ง)\n" +
            "• ระยะทาง: ยิ่งห่างจุดรั่ว รังสียิ่งลดลงเร็ว (แหล่งรังสีแบบจุด ห่างขึ้น 2 เท่า เหลือประมาณ 1/4)\n" +
            "• ที่กำบัง: ผนังหรือวัสดุหนาที่อยู่ระหว่างตัวเรากับแหล่งรังสีช่วยลดรังสีได้\n\n" +

            $"{H}5) อ่านค่า Geiger Counter (ที่ข้อมือซ้าย){E}\n" +
            "CPS = จำนวนคลิกต่อวินาที  ยิ่งคลิกถี่ = ยิ่งใกล้แหล่งรังสี\n" +
            $"{G}เขียว ต่ำกว่า 20{E} ปลอดภัย   <color=#FFC040>เหลือง 20-100{E} ระวัง   <color=#FF4A3A>แดง เกิน 100{E} อันตราย\n" +
            $"{R}ขีดจำกัด: ถ้ารังสีสะสมเกิน 25 µSv (ดูที่ HUD เหนือมือซ้าย) จะล้มเหลวและต้องเริ่มใหม่{E}\n\n" +
            "<size=24><color=#888888>* ตัวเลข % และค่ารังสีในเกมปรับให้เข้าใจง่ายสำหรับการสาธิต ไม่ใช่ค่าจริงทางการแพทย์</color></size>";

        panel.gameObject.SetActive(false);
        return panel.gameObject;
    }

    // ================================================================= rig

    static ShowcaseRig BuildRig(Transform start, Grabbable[] items, out Text hudText)
    {
        var root = new GameObject("Player Rig (VR + Desktop)");
        root.transform.SetPositionAndRotation(start.position, start.rotation);
        var cc = root.AddComponent<CharacterController>();
        cc.radius = 0.2f;
        cc.height = 1.7f;
        cc.center = new Vector3(0, 0.87f, 0);
        cc.stepOffset = 0.3f;
        cc.skinWidth = 0.02f;
        var rig = root.AddComponent<ShowcaseRig>();
        rig.items = items;

        var offset = new GameObject("CameraOffset").transform;
        offset.SetParent(root.transform, false);
        offset.localPosition = new Vector3(0, 1.6f, 0);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(offset, false);
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.03f;
        cam.farClipPlane = 100f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        camGo.AddComponent<AudioListener>();
        cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;

        Transform MakeHand(string name, float x)
        {
            var h = new GameObject(name).transform;
            h.SetParent(offset, false);
            h.localPosition = new Vector3(x, -0.45f, 0.35f);
            Box("HandOrb", h, Vector3.zero, new Vector3(0.06f, 0.06f, 0.06f), mHand, false, PrimitiveType.Sphere); // H3VR-style round hand
            return h;
        }

        rig.cameraOffset = offset;
        rig.head = camGo.transform;
        rig.leftHand = MakeHand("LeftHand", -0.2f);
        rig.rightHand = MakeHand("RightHand", 0.2f);

        var lineGo = new GameObject("TeleportLine");
        lineGo.transform.SetParent(root.transform, false);
        var line = lineGo.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.widthMultiplier = 0.012f;
        line.numCapVertices = 4;
        line.positionCount = 0;
        line.sharedMaterial = mLine;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.enabled = false;
        rig.line = line;

        var reticle = new GameObject("TeleportReticle").transform;
        reticle.SetParent(root.transform, false);
        Box("Disc", reticle, Vector3.zero, new Vector3(0.35f, 0.004f, 0.35f), mReticle, false, PrimitiveType.Cylinder);
        reticle.gameObject.SetActive(false);
        rig.reticle = reticle;

        rig.crosshair = Label("Crosshair", root.transform, Vector3.zero, Quaternion.identity,
            "+", 60, Color.white, new Vector2(80, 80), 0.0005f).transform.parent;

        var hud = MakeCanvas("HUD_Status", root.transform, Vector3.zero, Quaternion.identity,
            new Vector2(380, 230), 0.0007f, new Color(0, 0, 0, 0.7f));
        hudText = AddText(hud, "HudText", new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.97f), 30,
            Color.white, TextAnchor.MiddleLeft);
        hudText.text = "สถานะ";
        rig.hud = hud;

        return rig;
    }
    // ================================================================= geiger counter

    static GeigerCounter BuildGeigerCounter(ShowcaseRig rig, RadiationSource rad)
    {
        var root = new GameObject("GeigerCounter");
        root.transform.SetParent(rig.transform, false);
        var dev = root.transform;

        // body: probe points forward (+z), display on top (+y)
        Box("Body", dev, Vector3.zero, new Vector3(0.085f, 0.055f, 0.15f), mYellow, false);
        Box("Grip", dev, new Vector3(0, -0.05f, -0.02f), new Vector3(0.035f, 0.05f, 0.07f), mDark, false);
        Box("Speaker", dev, new Vector3(0.0435f, 0, -0.03f), new Vector3(0.003f, 0.03f, 0.05f), mDark, false);
        var probe = Box("Probe", dev, new Vector3(0, 0, 0.125f), new Vector3(0.03f, 0.05f, 0.03f), mMetal, false, PrimitiveType.Cylinder);
        probe.transform.localRotation = Quaternion.Euler(90, 0, 0);
        Box("ProbeWindow", dev, new Vector3(0, 0, 0.175f), new Vector3(0.032f, 0.032f, 0.012f), mDark, false, PrimitiveType.Sphere);
        var tip = new GameObject("ProbeTip").transform;
        tip.SetParent(dev, false);
        tip.localPosition = new Vector3(0, 0, 0.18f);
        Box("Bezel", dev, new Vector3(0, 0.0285f, -0.005f), new Vector3(0.072f, 0.004f, 0.11f), mDark, false);

        // display: 700 x 1000 px -> 7 x 10 cm, lying on the top face, text "up" toward the probe
        var disp = MakeCanvas("Display", dev, new Vector3(0, 0.031f, -0.005f), Quaternion.Euler(90, 0, 0),
            new Vector2(640, 1000), 0.0001f, new Color(0.05f, 0.09f, 0.06f, 1f));

        var title = AddText(disp, "Title", new Vector2(0.05f, 0.88f), new Vector2(0.8f, 0.98f), 60,
            new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleLeft);
        title.text = "GEIGER";

        var ledGo = new GameObject("LED", typeof(RectTransform), typeof(Image));
        var ledRt = (RectTransform)ledGo.transform;
        ledRt.SetParent(disp, false);
        ledRt.anchorMin = ledRt.anchorMax = new Vector2(0.88f, 0.93f);
        ledRt.sizeDelta = new Vector2(60, 60);
        var led = ledGo.GetComponent<Image>();

        // analog meter: pivot near the middle, scale marks on an arc (log scale 0 / 10 / 100 / 1k)
        var pivotPos = new Vector2(0f, -120f); // relative to canvas centre
        string[] marks = { "0", "10", "100", "1k" };
        float[] angles = { -60f, -20f, 20f, 60f };
        for (int i = 0; i < marks.Length; i++)
        {
            var m = new GameObject("Mark_" + marks[i], typeof(RectTransform), typeof(Text));
            var mrt = (RectTransform)m.transform;
            mrt.SetParent(disp, false);
            mrt.sizeDelta = new Vector2(120, 70);
            float rad2 = angles[i] * Mathf.Deg2Rad;
            mrt.anchoredPosition = pivotPos + new Vector2(Mathf.Sin(rad2), Mathf.Cos(rad2)) * 330f;
            var mt = m.GetComponent<Text>();
            mt.font = editorFont; mt.fontSize = 50; mt.alignment = TextAnchor.MiddleCenter;
            mt.color = i == 3 ? new Color(1f, 0.35f, 0.3f) : new Color(0.8f, 1f, 0.8f);
            mt.text = marks[i];
            texts.Add(mt);
        }

        var needleGo = new GameObject("Needle", typeof(RectTransform), typeof(Image));
        var needle = (RectTransform)needleGo.transform;
        needle.SetParent(disp, false);
        needle.pivot = new Vector2(0.5f, 0f);
        needle.sizeDelta = new Vector2(10, 290);
        needle.anchoredPosition = pivotPos;
        needle.localRotation = Quaternion.Euler(0, 0, 60);
        needleGo.GetComponent<Image>().color = new Color(1f, 0.3f, 0.2f);

        var hub = new GameObject("NeedleHub", typeof(RectTransform), typeof(Image));
        var hubRt = (RectTransform)hub.transform;
        hubRt.SetParent(disp, false);
        hubRt.sizeDelta = new Vector2(40, 40);
        hubRt.anchoredPosition = pivotPos;
        hub.GetComponent<Image>().color = new Color(0.8f, 0.8f, 0.8f);

        var readout = AddText(disp, "Readout", new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.25f), 120,
            Color.white, TextAnchor.MiddleCenter);
        readout.text = "0 CPS";

        root.AddComponent<AudioSource>();
        var geiger = root.AddComponent<GeigerCounter>();
        geiger.source = rad;
        geiger.rig = rig;
        geiger.probeTip = tip;
        geiger.needle = needle;
        geiger.readout = readout;
        geiger.led = led;
        return geiger;
    }

    // ================================================================= particles / volume

    static ParticleSystem CreateParticles(Transform vent)
    {
        var go = new GameObject("RadiationParticles");
        go.transform.SetParent(vent, false);
        go.transform.localPosition = new Vector3(0.12f, 2.3f, 0);
        go.transform.localRotation = Quaternion.LookRotation(new Vector3(1f, -0.25f, 0f));
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.duration = 5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3.5f, 5.5f);
        main.startSpeed = 1f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.4f);
        main.startColor = new Color(0.45f, 1f, 0.3f, 0.55f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 600;
        main.gravityModifier = 0.02f;

        var emission = ps.emission;
        emission.rateOverTime = 45f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 28f;
        shape.radius = 0.45f;

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.45f;
        noise.frequency = 0.4f;

        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.drag = 0.35f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(0.6f, 1f, 0.4f), 0f), new GradientColorKey(new Color(0.2f, 1f, 0.3f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.6f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = g;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.6f));

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mParticle;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return ps;
    }

    static void BuildVolume()
    {
        string path = GenDir + "/ShowcaseVolumeProfile.asset";
        AssetDatabase.DeleteAsset(path);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);
        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(0.9f);
        bloom.threshold.Override(1f);
        var vig = profile.Add<Vignette>(true);
        vig.intensity.Override(0.25f);
        foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
        EditorUtility.SetDirty(profile);

        var go = new GameObject("Global Volume");
        var v = go.AddComponent<Volume>();
        v.isGlobal = true;
        v.sharedProfile = profile;
    }

    // ================================================================= materials

    static void CreateMaterials()
    {
        mFloor = Lit("Floor", new Color(0.32f, 0.34f, 0.36f), null, 0.4f);
        mWall = Lit("Wall", new Color(0.62f, 0.64f, 0.62f), null, 0.2f);
        mCeiling = Lit("Ceiling", new Color(0.5f, 0.5f, 0.5f));
        mMetal = Lit("Metal", new Color(0.45f, 0.47f, 0.5f), null, 0.7f);
        mDark = Lit("DarkMetal", new Color(0.08f, 0.08f, 0.09f), null, 0.5f);
        mYellow = Lit("HazardYellow", new Color(0.95f, 0.75f, 0.1f));
        mOrangeBtn = Lit("ButtonOrange", new Color(1f, 0.55f, 0.1f), Color.black);
        mBlue = Lit("SwitchBlue", new Color(0.15f, 0.35f, 0.75f), Color.black);
        mRed = Lit("ButtonRed", new Color(0.8f, 0.1f, 0.1f), Color.black);
        mGreenPath = Lit("SafePathGreen", new Color(0.1f, 0.6f, 0.2f), new Color(0.2f, 1.6f, 0.4f));
        mDanger = Lit("DangerZoneRed", new Color(0.3f, 0.02f, 0.02f), Color.black);
        mVentGlow = Lit("VentGlow", new Color(0.3f, 1f, 0.3f), new Color(0.6f, 4f, 0.5f));
        mExit = Lit("ExitDoor", new Color(0.1f, 0.35f, 0.15f), new Color(0.05f, 0.4f, 0.1f));
        mLamp = Lit("Lamp", Color.white, new Color(2f, 1.9f, 1.7f));
        mBoard = Lit("BoardBack", new Color(0.06f, 0.07f, 0.09f));
        mHand = Lit("HandOrb", new Color(0.55f, 0.75f, 0.95f), null, 0.6f);
        mLine = SaveMat(new Material(Shader.Find("Sprites/Default")), "TeleportLine");
        mReticle = SaveMat(new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color")), "TeleportReticle");
        mReticle.SetColor("_BaseColor", Color.green);
        mSuit = Lit("RadSuit", new Color(0.95f, 0.75f, 0.1f), Color.black, 0.25f);
        mVisor = Lit("Visor", new Color(0.1f, 0.2f, 0.25f), null, 0.9f);
        mPill = Lit("PillBottle", new Color(1f, 0.45f, 0.05f), Color.black, 0.6f);
        mCap = Lit("PillCap", new Color(0.95f, 0.95f, 0.95f), Color.black, 0.4f);


        // additive particle material with a soft round texture
        var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = "SoftDot", wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f;
                float a = Mathf.Clamp01(1f - d);
                tex.SetPixel(x, y, new Color(1, 1, 1, a * a));
            }
        tex.Apply();
        string texPath = GenDir + "/SoftDot.asset";
        AssetDatabase.DeleteAsset(texPath);
        AssetDatabase.CreateAsset(tex, texPath);

        var pShader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
        var p = new Material(pShader);
        p.SetFloat("_Surface", 1f);
        p.SetFloat("_Blend", 2f); // additive
        p.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        p.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        p.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        p.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        p.SetFloat("_ZWrite", 0f);
        p.SetOverrideTag("RenderType", "Transparent");
        p.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        p.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        p.SetTexture("_BaseMap", tex);
        p.SetColor("_BaseColor", new Color(0.6f, 1.6f, 0.6f, 1f));
        mParticle = SaveMat(p, "RadiationParticle");
    }

    static Material Lit(string name, Color color, Color? emission = null, float smoothness = 0.3f)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(shader);
        m.SetColor("_BaseColor", color);
        m.SetColor("_Color", color);
        m.SetFloat("_Smoothness", smoothness);
        if (emission.HasValue)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission.Value);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        return SaveMat(m, name);
    }

    static Material SaveMat(Material m, string name)
    {
        string path = $"{GenDir}/{name}.mat";
        AssetDatabase.DeleteAsset(path);
        m.name = name;
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // ================================================================= helpers

    static GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat,
        bool collider = true, PrimitiveType type = PrimitiveType.Cube)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static void Strip(Transform parent, Vector3 a, Vector3 b, float width, Material mat)
    {
        Vector3 d = b - a;
        var go = Box("PathStrip", parent, (a + b) * 0.5f + Vector3.up * 0.006f, new Vector3(width, 0.01f, d.magnitude + width), mat, false);
        go.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
    }

    static RectTransform MakeCanvas(string name, Transform parent, Vector3 localPos, Quaternion localRot,
        Vector2 sizePx, float metersPerPixel, Color? background)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas));
        var rt = (RectTransform)go.transform;
        if (parent != null) rt.SetParent(parent, false);
        go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        rt.sizeDelta = sizePx;
        rt.localPosition = localPos;
        rt.localRotation = localRot;
        rt.localScale = Vector3.one * metersPerPixel;

        if (background.HasValue)
        {
            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            var brt = (RectTransform)bg.transform;
            brt.SetParent(rt, false);
            Stretch(brt, Vector2.zero, Vector2.one, 0);
            bg.GetComponent<Image>().color = background.Value;
        }
        return rt;
    }

    static Text AddText(RectTransform canvas, string name, Vector2 anchorMin, Vector2 anchorMax, int fontSize,
        Color color, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas, false);
        Stretch(rt, anchorMin, anchorMax, 0);
        var t = go.GetComponent<Text>();
        t.font = editorFont;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = align;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.lineSpacing = 1.1f;
        texts.Add(t);
        return t;
    }

    static Text Label(string name, Transform parent, Vector3 localPos, Quaternion localRot, string text, int fontSize,
        Color color, Vector2 sizePx, float metersPerPixel, Color? background = null)
    {
        var canvas = MakeCanvas(name, parent, localPos, localRot, sizePx, metersPerPixel, background);
        var t = AddText(canvas, "Text", Vector2.zero, Vector2.one, fontSize, color, TextAnchor.MiddleCenter);
        t.text = text;
        return t;
    }

    static void Stretch(RectTransform rt, Vector2 min, Vector2 max, float pad)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = new Vector2(pad, pad);
        rt.offsetMax = new Vector2(-pad, -pad);
    }

    static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
    }

    static void AddSceneToBuildSettings()
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        list.RemoveAll(s => s.path == ScenePath);
        list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}
