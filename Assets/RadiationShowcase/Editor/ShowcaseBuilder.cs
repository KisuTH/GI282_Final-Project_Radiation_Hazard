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
        mDanger, mVentGlow, mExit, mLamp, mBoard, mHand, mParticle, mSuit, mVisor, mPill, mCap, mLine, mReticle, mGlass, mAxeHandle, mBeacon, mCrate;

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
        // 20 m long: x from -4 (start / preparation end) to +16 (exit end)
        Box("Floor", env, new Vector3(6, -0.05f, 0), new Vector3(20, 0.1f, 6), mFloor).AddComponent<TeleportArea>();
        Box("Ceiling", env, new Vector3(6, 3.05f, 0), new Vector3(20, 0.1f, 6), mCeiling);
        Box("Wall_North", env, new Vector3(6, 1.5f, 3.1f), new Vector3(20.4f, 3, 0.2f), mWall);
        Box("Wall_South", env, new Vector3(6, 1.5f, -3.1f), new Vector3(20.4f, 3, 0.2f), mWall);
        Box("Wall_West", env, new Vector3(-4.1f, 1.5f, 0), new Vector3(0.2f, 3, 6), mWall);
        Box("Wall_East", env, new Vector3(16.1f, 1.5f, 0), new Vector3(0.2f, 3, 6), mWall);
        Box("Stripe_N", env, new Vector3(6, 0.1f, 2.99f), new Vector3(20, 0.2f, 0.02f), mYellow, false);
        Box("Stripe_S", env, new Vector3(6, 0.1f, -2.99f), new Vector3(20, 0.2f, 0.02f), mYellow, false);

        // zig-zag: four rows of tall crate stacks, openings alternate north / south / north / south
        var crates = new GameObject("ZigZagCrates").transform;
        var rnd = new System.Random(7);
        foreach (var (rowX, gapNorth) in new[] { (5f, true), (7.5f, false), (10f, true), (12.5f, false) })
        {
            float[] zs = gapNorth ? new[] { -2.5f, -1.5f, -0.5f, 0.5f } : new[] { 2.5f, 1.5f, 0.5f, -0.5f };
            foreach (var z in zs)
            {
                var stack = new GameObject("CrateStack").transform;
                stack.SetParent(crates, false);
                stack.position = new Vector3(rowX, 0, z);
                stack.rotation = Quaternion.Euler(0, (float)(rnd.NextDouble() * 10 - 5), 0);
                Box("Crate", stack, new Vector3(0, 0.6f, 0), new Vector3(1f, 1.2f, 1f), mCrate);
                var top = Box("Crate", stack, new Vector3(0, 1.75f, 0), new Vector3(0.92f, 1.1f, 0.92f), mCrate);
                top.transform.localRotation = Quaternion.Euler(0, (float)(rnd.NextDouble() * 16 - 8), 0);
                Box("Band", stack, new Vector3(0, 0.95f, 0), new Vector3(1.02f, 0.08f, 1.02f), mYellow, false);
            }
        }

        foreach (var x in new[] { -2f, 2f, 6f, 10f, 14f })
        {
            Box("Lamp", env, new Vector3(x, 2.98f, 0), new Vector3(1.2f, 0.04f, 0.3f), mLamp, false);
            var l = new GameObject("CeilingLight").AddComponent<Light>();
            l.transform.SetParent(env, false);
            l.transform.localPosition = new Vector3(x, 2.7f, 0);
            l.type = LightType.Point; l.range = 8f; l.intensity = 1.6f;
            l.color = new Color(1f, 0.95f, 0.85f); l.shadows = LightShadows.None;
        }

        // ------------------------------------------------------------ vent + radiation (west wall, right behind the spawn point)
        // local +X points into the room (east, toward the exit)
        var radGo = new GameObject("RadiationSource (Air Vent)");
        radGo.transform.SetPositionAndRotation(new Vector3(-3.9f, 0, -0.6f), Quaternion.identity);
        var rad = radGo.AddComponent<RadiationSource>();
        var vent = radGo.transform;

        Box("VentFrame", vent, new Vector3(-0.04f, 2.3f, 0), new Vector3(0.12f, 0.9f, 1.3f), mMetal);
        var ventGlow = Box("VentGlow", vent, new Vector3(0.025f, 2.3f, 0), new Vector3(0.01f, 0.7f, 1.1f), mVentGlow, false);
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

        var ps = CreateParticles(vent);

        // the contamination starts small behind the player and chases them down the room toward the exit
        rad.maxRateSvPerHour = 2000f;
        rad.falloffPower = 4f;
        rad.range = 22f;
        rad.dangerRadius = 11f;
        rad.startSize = 0.1f;
        rad.spreadDuration = 40f;
        rad.particles = ps;
        rad.glowLight = glow;
        rad.ventGlow = ventGlow.GetComponent<Renderer>();
        rad.fanBlades = blades;

        // ------------------------------------------------------------ Step 2: radiation suit on a rack (behind crate row 1, south wall)
        var rack = new GameObject("Step1_SuitRack").transform;
        rack.SetPositionAndRotation(new Vector3(6.2f, 0, -2.75f), Quaternion.identity); // south wall, local +Z = into the room
        Box("Post_L", rack, new Vector3(-0.5f, 1.0f, -0.1f), new Vector3(0.05f, 2.0f, 0.05f), mMetal);
        Box("Post_R", rack, new Vector3(0.5f, 1.0f, -0.1f), new Vector3(0.05f, 2.0f, 0.05f), mMetal);
        Box("Bar", rack, new Vector3(0, 2.0f, -0.1f), new Vector3(1.05f, 0.04f, 0.04f), mMetal);
        Box("Hanger", rack, new Vector3(0, 1.96f, 0.05f), new Vector3(0.02f, 0.1f, 0.02f), mDark, false);
        Label("RackSign", rack, new Vector3(0, 2.3f, 0), Quaternion.Euler(0, 180, 0), // face the player (who looks toward the wall)
            "STEP 2\nชุดป้องกันรังสี", 60, Color.black, new Vector2(900, 240), 0.001f, new Color(0.95f, 0.75f, 0.1f, 0.95f));

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
        suit.gripPoint = Grip(st, new Vector3(0, 0.3f, 0), Vector3.forward, Vector3.up);
        suit.startKinematic = true;
        suit.actionId = "suit";

        // ------------------------------------------------------------ Step 4: fan lever (east wall, next to the exit)
        var sw = new GameObject("Step4_FanLever").transform;
        sw.SetPositionAndRotation(new Vector3(15.95f, 0, -1.2f), Quaternion.Euler(0, 90, 0)); // panel front (local -Z) faces west, into the room
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
            "STEP 4\nจับคันโยกแล้วโยกลง = ปิดพัดลม", 52, Color.white, new Vector2(1000, 240), 0.001f, new Color(0.05f, 0.15f, 0.35f, 0.9f));

        // ------------------------------------------------------------ Step 3: radiation blocker pills on a table (behind crate row 2, north wall)
        var table = new GameObject("Step3_PillTable").transform;
        table.SetPositionAndRotation(new Vector3(8.75f, 0, 2.45f), Quaternion.Euler(0, 180, 0)); // local +Z = into the room
        Box("TableTop", table, new Vector3(0, 0.8f, 0), new Vector3(0.9f, 0.05f, 0.5f), mMetal);
        foreach (var lx in new[] { -0.4f, 0.4f })
            foreach (var lz in new[] { -0.2f, 0.2f })
                Box("Leg", table, new Vector3(lx, 0.39f, lz), new Vector3(0.04f, 0.78f, 0.04f), mDark);
        Label("PillSign", table, new Vector3(0, 1.25f, -0.2f), Quaternion.Euler(0, 180, 0),
            "STEP 3\nยา Radiation Blocker\nPotassium Iodide (KI)\nหยิบขวดแล้วยกขึ้นที่ปาก", 46, Color.white, new Vector2(900, 360), 0.001f,
            new Color(0.4f, 0.15f, 0.02f, 0.9f));

        var pillGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillGo.name = "PillBottle (grab + bring to mouth)";
        pillGo.transform.SetPositionAndRotation(table.TransformPoint(0, 0.886f, 0.1f), table.rotation);
        pillGo.transform.localScale = new Vector3(0.06f, 0.06f, 0.06f);
        pillGo.GetComponent<Renderer>().sharedMaterial = mPill;
        var cap = Box("Cap", pillGo.transform, new Vector3(0, 1.15f, 0), new Vector3(1.05f, 0.2f, 1.05f), mCap, false, PrimitiveType.Cylinder);
        var pillRb = pillGo.AddComponent<Rigidbody>();
        pillRb.mass = 0.2f;
        pillRb.interpolation = RigidbodyInterpolation.Interpolate;
        pillRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        pillRb.isKinematic = true;
        var pill = pillGo.AddComponent<ConsumableItem>();
        pill.startKinematic = true;   // stays put on the table until picked up (also after a reset)
        pill.gripPoint = Grip(pillGo.transform, Vector3.zero, Vector3.forward, Vector3.up);
        pill.actionId = "pill";
        pill.glowRenderers = new[] { pillGo.GetComponent<Renderer>(), cap.GetComponent<Renderer>() };

        // ------------------------------------------------------------ Exit (east wall, far end of the room)
        var exitGrp = new GameObject("Exit").transform;
        exitGrp.position = new Vector3(15.95f, 0, 1.8f);
        Box("Door", exitGrp, new Vector3(0, 1.05f, 0), new Vector3(0.06f, 2.1f, 1.1f), mExit);
        Box("Frame_L", exitGrp, new Vector3(-0.02f, 1.1f, -0.6f), new Vector3(0.1f, 2.2f, 0.1f), mMetal);
        Box("Frame_R", exitGrp, new Vector3(-0.02f, 1.1f, 0.6f), new Vector3(0.1f, 2.2f, 0.1f), mMetal);
        Box("Frame_T", exitGrp, new Vector3(-0.02f, 2.2f, 0), new Vector3(0.1f, 0.1f, 1.3f), mMetal);
        Label("ExitSign", exitGrp, new Vector3(-0.08f, 2.5f, 0), Quaternion.Euler(0, 90, 0),
            "EXIT  /  ทางออก", 80, new Color(0.35f, 1f, 0.5f), new Vector2(1100, 200), 0.001f, new Color(0, 0.1f, 0.03f, 0.9f));
        Box("ExitPad", env, new Vector3(15.35f, 0.008f, 1.8f), new Vector3(1.2f, 0.01f, 1.4f), mGreenPath, false);
        var exitPoint = new GameObject("ExitPoint").transform;
        exitPoint.position = new Vector3(15.3f, 0, 1.8f);
        var exitLight = new GameObject("ExitLight").AddComponent<Light>();
        exitLight.transform.position = new Vector3(15.5f, 2.6f, 1.8f);
        exitLight.type = LightType.Point; exitLight.range = 4f; exitLight.intensity = 1.5f;
        exitLight.color = new Color(0.3f, 1f, 0.45f); exitLight.shadows = LightShadows.None;

        // ------------------------------------------------------------ guide path (green): axe + case -> suit -> pills -> lever -> exit
        var path = new GameObject("GuidePath").transform;
        var pts = new[]
        {
            new Vector3(-2.4f, 0, -0.6f), new Vector3(-0.9f, 0, -2.2f), new Vector3(-1.8f, 0, -2.2f), new Vector3(5f, 0, 2.1f),
            new Vector3(6.2f, 0, -2.0f), new Vector3(7.5f, 0, -2.1f), new Vector3(8.5f, 0, -1.8f), new Vector3(8.75f, 0, 1.7f),
            new Vector3(10f, 0, 2.1f), new Vector3(12.5f, 0, -2.1f), new Vector3(15.2f, 0, -1.2f), new Vector3(15.3f, 0, 1.8f)
        };
        for (int i = 0; i < pts.Length - 1; i++) Strip(path, pts[i], pts[i + 1], 0.12f, mGreenPath);

        // ------------------------------------------------------------ start point
        var start = new GameObject("StartPoint").transform;
        start.position = new Vector3(-2.4f, 0, -0.6f);
        start.rotation = Quaternion.Euler(0, 90, 0);   // facing east down the room: the leaking vent is right behind you

        // ------------------------------------------------------------ guide board (north wall, in front of the start)
        var boardGrp = new GameObject("GuideBoard").transform;
        boardGrp.position = new Vector3(-2.3f, 0, 2.98f);
        Box("BoardBack", boardGrp, new Vector3(0, 1.7f, 0), new Vector3(2.0f, 1.4f, 0.04f), mBoard);
        var boardCanvas = MakeCanvas("BoardCanvas", boardGrp, new Vector3(0, 1.7f, -0.03f), Quaternion.identity,
            new Vector2(1900, 1300), 0.001f, null);
        AddText(boardCanvas, "Title", new Vector2(0.03f, 0.88f), new Vector2(0.97f, 0.98f), 70,
            new Color(1f, 0.85f, 0.25f), TextAnchor.MiddleLeft).text = "คู่มือเอาตัวรอด: รังสีรั่วไหลที่ประตูทางออก";
        var stepsText = AddText(boardCanvas, "Steps", new Vector2(0.04f, 0.30f), new Vector2(0.96f, 0.87f), 38,
            Color.white, TextAnchor.UpperLeft);
        stepsText.text = "Step 1 ...";
        var statusText = AddText(boardCanvas, "Status", new Vector2(0.04f, 0.18f), new Vector2(0.96f, 0.29f), 38,
            new Color(1f, 0.6f, 0.3f), TextAnchor.MiddleLeft);
        AddText(boardCanvas, "Controls", new Vector2(0.04f, 0.01f), new Vector2(0.74f, 0.17f), 26,
            new Color(0.75f, 0.75f, 0.75f), TextAnchor.MiddleLeft).text =
            "VR (แบบ H3VR): สติ๊กซ้าย = เดิน, กดสติ๊กซ้าย = เปิด/ปิดวิ่ง | สติ๊กขวาซ้าย-ขวา = หมุน 45° | สติ๊กขวาดันขึ้น = เล็งวาร์ป ปล่อย = วาร์ป\n" +
            "Grip ค้าง = จับ/ถือ | ชี้ของไกล + Trigger + Grip = ดูดของเข้ามือ | เอามือจิ้มปุ่ม = กด | X = ผู้ช่วย AI | Y = คู่มือ | Menu = หยุดเกม / เริ่มใหม่\n" +
            "PC: คลิกล็อกเมาส์ | WASD, Shift = วิ่ง | คลิกขวาค้าง = วาร์ป | คลิกซ้ายค้าง = จับ | F = เข้าตัว/ปาก | Space = เหวี่ยงขวาน | G = วาง Geiger | Tab = ผู้ช่วย AI | H = คู่มือ | R = รีเซ็ต | Esc = หยุดเกม";
        var boardResetGo = Box("Button_Reset", boardGrp, new Vector3(0.75f, 1.12f, -0.05f), new Vector3(0.32f, 0.14f, 0.06f), mRed);
        var boardReset = boardResetGo.AddComponent<WorldButton>();
        boardReset.id = "reset";
        boardReset.emissionColor = new Color(1f, 0.2f, 0.2f);
        Label("ResetLabel", boardGrp, new Vector3(0.75f, 1.12f, -0.085f), Quaternion.identity,
            "RESET", 70, Color.white, new Vector2(320, 140), 0.001f);

        // ------------------------------------------------------------ emergency case with the Geiger counter (south wall, next to the spawn)
        var caseGrp = new GameObject("Step1_EmergencyCase (break the glass)").transform;
        caseGrp.SetPositionAndRotation(new Vector3(-1.8f, 0, -2.92f), Quaternion.identity); // local +Z = into the room
        Box("CaseBack", caseGrp, new Vector3(0, 1.3f, 0), new Vector3(0.7f, 0.62f, 0.04f), mRed);
        Box("Frame_L", caseGrp, new Vector3(-0.33f, 1.3f, 0.14f), new Vector3(0.04f, 0.62f, 0.28f), mRed);
        Box("Frame_R", caseGrp, new Vector3(0.33f, 1.3f, 0.14f), new Vector3(0.04f, 0.62f, 0.28f), mRed);
        Box("Frame_T", caseGrp, new Vector3(0, 1.59f, 0.14f), new Vector3(0.7f, 0.04f, 0.28f), mRed);
        Box("Frame_B", caseGrp, new Vector3(0, 1.01f, 0.14f), new Vector3(0.7f, 0.04f, 0.28f), mRed);
        Box("Bracket", caseGrp, new Vector3(0, 1.3f, 0.05f), new Vector3(0.12f, 0.2f, 0.06f), mDark, false);
        var pane = Box("GlassPane", caseGrp, new Vector3(0, 1.3f, 0.27f), new Vector3(0.62f, 0.54f, 0.012f), mGlass);
        Label("CaseSign", caseGrp, new Vector3(0, 1.8f, 0.05f), Quaternion.Euler(0, 180, 0),
            "STEP 1  EMERGENCY\nทุบกระจกเพื่อหยิบ Geiger Counter", 48, Color.white, new Vector2(1000, 220), 0.001f,
            new Color(0.7f, 0.05f, 0.05f, 0.95f));

        var geiger = BuildGeigerCounter(rad);
        geiger.transform.SetPositionAndRotation(caseGrp.TransformPoint(0, 1.3f, 0.14f),
            caseGrp.rotation * Quaternion.LookRotation(Vector3.up, Vector3.forward)); // probe up, display facing out of the case

        // ------------------------------------------------------------ fire axe on the wall next to the case
        var axeMount = new GameObject("FireAxeMount").transform;
        axeMount.SetPositionAndRotation(new Vector3(-0.9f, 0, -2.92f), Quaternion.identity);
        Box("MountPlate", axeMount, new Vector3(0, 1.3f, 0), new Vector3(0.35f, 0.95f, 0.02f), mRed, false);
        Label("AxeSign", axeMount, new Vector3(0, 1.9f, 0.02f), Quaternion.Euler(0, 180, 0),
            "FIRE AXE\nขวานดับเพลิง", 44, Color.white, new Vector2(420, 160), 0.001f, new Color(0.6f, 0.05f, 0.05f, 0.95f));
        var axeGo = new GameObject("FireAxe (grab + swing at the glass)");
        axeGo.transform.SetPositionAndRotation(axeMount.TransformPoint(0, 1.3f, 0.06f), axeMount.rotation);
        var ax = axeGo.transform;
        var handle = Box("Handle", ax, Vector3.zero, new Vector3(0.035f, 0.4f, 0.035f), mAxeHandle, false, PrimitiveType.Cylinder);
        var headBox = Box("Head", ax, new Vector3(0.03f, 0.37f, 0), new Vector3(0.16f, 0.08f, 0.03f), mMetal, false);
        var blade = Box("Blade", ax, new Vector3(0.12f, 0.37f, 0), new Vector3(0.05f, 0.14f, 0.012f), mMetal, false);
        Box("Pick", ax, new Vector3(-0.08f, 0.37f, 0), new Vector3(0.08f, 0.025f, 0.02f), mDark, false);
        var axeHead = new GameObject("HeadPoint").transform;
        axeHead.SetParent(ax, false);
        axeHead.localPosition = new Vector3(0.14f, 0.37f, 0);
        var axeCol = axeGo.AddComponent<BoxCollider>();
        axeCol.center = new Vector3(0.02f, 0.02f, 0);
        axeCol.size = new Vector3(0.3f, 0.84f, 0.06f);
        var axeRb = axeGo.AddComponent<Rigidbody>();
        axeRb.mass = 1.5f;
        axeRb.interpolation = RigidbodyInterpolation.Interpolate;
        axeRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        axeRb.isKinematic = true;
        var axe = axeGo.AddComponent<Grabbable>();
        axe.gripPoint = Grip(ax, new Vector3(0, -0.28f, 0), Vector3.up, Vector3.left);
        axe.startKinematic = true;
        axe.glowRenderers = new[] { handle.GetComponent<Renderer>(), headBox.GetComponent<Renderer>(), blade.GetComponent<Renderer>() };

        var glass = caseGrp.gameObject.AddComponent<BreakableGlass>();
        glass.axe = axe;
        glass.axeHead = axeHead;
        glass.geiger = geiger;
        glass.pane = pane;
        glass.shardMaterial = mGlass;

        // ------------------------------------------------------------ handbook (Y button)
        var handbook = BuildHandbook();

        // ------------------------------------------------------------ player rig
        var interactables = new Grabbable[] { geiger, axe, suit, leverHandle, pill };
        var rig = BuildRig(start, interactables);
        rad.listener = rig.head;
        rig.handbook = handbook;
        var cam = rig.head.GetComponent<Camera>();

        // ------------------------------------------------------------ fade overlay (child of the camera) + end message (UI layer)
        var fadeCanvas = MakeCanvas("FadeOverlay", rig.head, new Vector3(0, 0, 0.1f), Quaternion.identity,
            new Vector2(1000, 1000), 0.0004f, null);
        var fadeGo = new GameObject("Black", typeof(RectTransform), typeof(Image));
        var fadeRt = (RectTransform)fadeGo.transform;
        fadeRt.SetParent(fadeCanvas, false);
        Stretch(fadeRt, Vector2.zero, Vector2.one, 0);
        var fadeImage = fadeGo.GetComponent<Image>();
        fadeImage.color = new Color(0, 0, 0, 0);
        fadeImage.raycastTarget = false;
        fadeImage.enabled = false;

        var endPanel = MakeCanvas("EndMessage (UI layer)", null, new Vector3(0, 1.5f, 0), Quaternion.identity,
            new Vector2(1500, 1000), 0.0009f, null);
        var endText = AddText(endPanel, "EndText", new Vector2(0.03f, 0.2f), new Vector2(0.97f, 0.97f), 44,
            Color.white, TextAnchor.MiddleCenter);
        endText.text = "END";
        foreach (var (btnId, btnLabel, btnX, btnMat, btnGlow) in new[]
        {
            ("restart", "เริ่มใหม่", -300f, mOrangeBtn, new Color(1f, 0.6f, 0.1f)),
            ("quit", "ออกจากเกม", 300f, mRed, new Color(1f, 0.2f, 0.2f)),
        })
        {
            var btnGo = Box("Button_" + btnId, endPanel, new Vector3(btnX, -400, -40), new Vector3(520, 140, 60), btnMat);
            var btn = btnGo.AddComponent<WorldButton>();
            btn.id = btnId;
            btn.emissionColor = btnGlow;
            Label("Label_" + btnId, endPanel, new Vector3(btnX, -400, -75), Quaternion.identity,
                btnLabel, 72, Color.white, new Vector2(520, 140), 1f);
        }
        SetLayerRecursive(endPanel.gameObject, 5);
        endPanel.gameObject.SetActive(false);

        // ------------------------------------------------------------ post processing (+ "blacking out" with dose)
        var volume = BuildVolume();

        // ------------------------------------------------------------ game flow
        var flowGo = new GameObject("GameFlow");
        var exposure = flowGo.AddComponent<ExposureEffects>();
        exposure.volume = volume;
        exposure.radiation = rad;
        var flow = flowGo.AddComponent<GameFlow>();
        flow.rig = rig;
        flow.radiation = rad;
        flow.exposureEffects = exposure;
        flow.glass = glass;
        flow.startPoint = start;
        flow.exitPoint = exitPoint;
        flow.interactables = interactables;
        flow.interactableIds = new[] { "geiger", "geiger", "suit", "fan", "pill" };
        flow.fanStatusLabel = fanLabel;
        flow.boardSteps = stepsText;
        flow.boardStatus = statusText;
        flow.playerCamera = cam;
        flow.fadeImage = fadeImage;
        flow.endPanel = endPanel.gameObject;
        flow.endText = endText;

        // ------------------------------------------------------------ emergency alarm: rotating red beacons + siren
        BuildEmergencyAlarm(rad, flow);

        // ------------------------------------------------------------ offline assistant tablet (left wrist / bottom-left on desktop)
        var assistant = flowGo.AddComponent<RadiationAssistant>();
        assistant.rig = rig;
        assistant.flow = flow;
        assistant.radiation = rad;
        assistant.geiger = geiger;
        assistant.axe = axe;
        assistant.glass = glass;
        assistant.exitPoint = exitPoint;
        BuildAssistantTablet(assistant);

        // ------------------------------------------------------------ pause menu (Menu button / Esc)
        var pause = flowGo.AddComponent<PauseMenu>();
        pause.rig = rig;
        pause.flow = flow;
        pause.panel = BuildPausePanel();
        pause.mainPanel = BuildMainMenuPanel();

        flow.allTexts = texts.ToArray();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings();
        AssetDatabase.SaveAssets();
        Debug.Log($"[Radiation Showcase] Scene built: {ScenePath}");
    }

    // ================================================================= emergency alarm

    static void BuildEmergencyAlarm(RadiationSource rad, GameFlow flow)
    {
        var alarmGo = new GameObject("EmergencyAlarm (beacons + siren)");
        var alarm = alarmGo.AddComponent<EmergencyAlarm>();
        alarm.radiation = rad;
        alarm.flow = flow;

        var rotors = new List<Transform>();
        var lights = new List<Light>();
        var domes = new List<Renderer>();
        // one beacon high on the north wall, one on the south wall
        foreach (var (bPos, bYaw) in new[] { (new Vector3(-0.8f, 2.7f, 2.86f), 180f), (new Vector3(6f, 2.7f, -2.86f), 0f), (new Vector3(12f, 2.7f, 2.86f), 180f) })
        {
            var b = new GameObject("Beacon").transform;
            b.SetParent(alarmGo.transform, false);
            b.SetPositionAndRotation(bPos, Quaternion.Euler(0, bYaw, 0));
            Box("Bracket", b, new Vector3(0, -0.03f, -0.06f), new Vector3(0.22f, 0.04f, 0.26f), mDark, false);
            Box("Base", b, Vector3.zero, new Vector3(0.18f, 0.06f, 0.18f), mDark, false);
            var dome = Box("Dome", b, new Vector3(0, 0.1f, 0), new Vector3(0.16f, 0.18f, 0.16f), mBeacon, false, PrimitiveType.Sphere);
            domes.Add(dome.GetComponent<Renderer>());

            var rotor = new GameObject("Rotor").transform;
            rotor.SetParent(b, false);
            rotor.localPosition = new Vector3(0, 0.1f, 0);
            rotors.Add(rotor);
            foreach (var yaw in new[] { 0f, 180f })
            {
                var l = new GameObject("Beam").AddComponent<Light>();
                l.transform.SetParent(rotor, false);
                l.transform.localRotation = Quaternion.Euler(12f, yaw, 0);
                l.type = LightType.Spot;
                l.spotAngle = 45f;
                l.range = 9f;
                l.intensity = 8f;
                l.color = new Color(1f, 0.1f, 0.05f);
                l.shadows = LightShadows.None;
                lights.Add(l);
            }
        }
        alarm.rotors = rotors.ToArray();
        alarm.beamLights = lights.ToArray();
        alarm.domes = domes.ToArray();
    }

    // ================================================================= offline assistant tablet

    static void BuildAssistantTablet(RadiationAssistant assistant)
    {
        // 900 x 1000 px at 0.00035 m/px = 31.5 x 35 cm tablet
        var tab = MakeCanvas("AssistantTablet (offline AI)", null, new Vector3(0, 1.2f, 0), Quaternion.identity,
            new Vector2(900, 1000), 0.00035f, new Color(0.02f, 0.05f, 0.08f, 0.9f));

        AddText(tab, "Title", new Vector2(0.04f, 0.9f), new Vector2(0.96f, 0.98f), 46,
            new Color(0.4f, 0.9f, 1f), TextAnchor.MiddleLeft).text = "ผู้ช่วย AI <size=28><color=#88AABB>(ออฟไลน์ · X / Tab = พับเก็บ)</color></size>";
        var live = AddText(tab, "LiveAdvice", new Vector2(0.04f, 0.62f), new Vector2(0.96f, 0.89f), 38,
            Color.white, TextAnchor.UpperLeft);
        live.text = "...";
        var answer = AddText(tab, "Answer", new Vector2(0.04f, 0.33f), new Vector2(0.96f, 0.61f), 32,
            new Color(0.85f, 0.9f, 0.95f), TextAnchor.UpperLeft);
        answer.text = "<color=#AAAAAA>แตะปุ่มด้านล่างเพื่อถาม (PC: กด 1–4)  ·  X / Tab = พับเก็บ</color>";

        var questions = new[]
        {
            ("ask_next", "1  ต้องทำอะไรต่อ?", -220f, -260f),
            ("ask_safe", "2  จุดนี้ปลอดภัยไหม?", 220f, -260f),
            ("ask_gear", "3  อุปกรณ์ของฉัน?", -220f, -410f),
            ("ask_body", "4  ร่างกายฉันเป็นไง?", 220f, -410f),
        };
        foreach (var (qId, qLabel, qx, qy) in questions)
        {
            var qGo = Box("Ask_" + qId, tab, new Vector3(qx, qy, -15), new Vector3(420, 120, 30), mBlue);
            var qBtn = qGo.AddComponent<WorldButton>();
            qBtn.id = qId;
            qBtn.emissionColor = new Color(0.3f, 0.7f, 1f);
            Label("Label_" + qId, tab, new Vector3(qx, qy, -35), Quaternion.identity,
                qLabel, 36, Color.white, new Vector2(420, 120), 1f);
        }

        assistant.tablet = tab;
        assistant.liveText = live;
        assistant.answerText = answer;
    }

    // ================================================================= pause menu

    static GameObject BuildPausePanel()
    {
        // 900 x 1000 px at 0.0009 m/px = 0.81 x 0.9 m, ~1.4 m in front of the player (laser pointer in VR)
        var panel = MakeCanvas("PauseMenu", null, new Vector3(0, 1.4f, 0), Quaternion.identity,
            new Vector2(900, 1000), 0.0009f, new Color(0.03f, 0.04f, 0.06f, 0.96f));

        AddText(panel, "Title", new Vector2(0.05f, 0.83f), new Vector2(0.95f, 0.97f), 80,
            new Color(1f, 0.85f, 0.25f), TextAnchor.MiddleCenter).text = "หยุดเกมชั่วคราว";

        void MakeButton(string id, string label, float y, Material mat, Color glow)
        {
            var go = Box("Button_" + id, panel, new Vector3(0, y, -40), new Vector3(620, 150, 60), mat);
            var b = go.AddComponent<WorldButton>();
            b.id = id;
            b.emissionColor = glow;
            Label("Label_" + id, panel, new Vector3(0, y, -75), Quaternion.identity,
                label, 72, Color.white, new Vector2(620, 150), 1f);
        }
        MakeButton("resume", "เล่นต่อ", 220, mGreenPath, new Color(0.2f, 1f, 0.4f));
        MakeButton("restart", "เริ่มใหม่", 20, mOrangeBtn, new Color(1f, 0.6f, 0.1f));
        MakeButton("quit", "ออกจากเกม", -180, mRed, new Color(1f, 0.2f, 0.2f));

        AddText(panel, "Hint", new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.15f), 32,
            new Color(0.7f, 0.7f, 0.7f), TextAnchor.MiddleCenter).text =
            "VR: ชี้เลเซอร์ที่ปุ่มแล้วกด Trigger  |  กดปุ่ม Menu อีกครั้ง = เล่นต่อ\nPC: คลิกที่ปุ่ม  |  Esc = เล่นต่อ";

        SetLayerRecursive(panel.gameObject, 5);
        panel.gameObject.SetActive(false);
        return panel.gameObject;
    }

    // ================================================================= main menu (title screen)

    static GameObject BuildMainMenuPanel()
    {
        // 1100 x 1050 px at 0.001 m/px = 1.1 x 1.05 m, ~1.4 m in front of the player (laser pointer in VR)
        var panel = MakeCanvas("MainMenu (title screen)", null, new Vector3(0, 1.4f, 0), Quaternion.identity,
            new Vector2(1100, 1050), 0.001f, new Color(0.02f, 0.03f, 0.04f, 0.97f));

        AddText(panel, "Warning", new Vector2(0.05f, 0.88f), new Vector2(0.95f, 0.97f), 40,
            new Color(1f, 0.35f, 0.25f), TextAnchor.MiddleCenter).text = "!! RADIATION HAZARD !!";
        AddText(panel, "Title", new Vector2(0.04f, 0.62f), new Vector2(0.96f, 0.88f), 96,
            new Color(1f, 0.85f, 0.2f), TextAnchor.MiddleCenter).text = "How to survive:\nRadiation Leak";
        AddText(panel, "Subtitle", new Vector2(0.05f, 0.53f), new Vector2(0.95f, 0.62f), 46,
            new Color(0.75f, 1f, 0.75f), TextAnchor.MiddleCenter).text = "เอาชีวิตรอดจากรังสีรั่วไหล";

        var startGo = Box("Button_start", panel, new Vector3(0, -40, -40), new Vector3(640, 150, 60), mGreenPath);
        var startBtn = startGo.AddComponent<WorldButton>();
        startBtn.id = "start";
        startBtn.emissionColor = new Color(0.2f, 1f, 0.4f);
        Label("Label_start", panel, new Vector3(0, -40, -75), Quaternion.identity,
            "เริ่มเกม", 80, Color.white, new Vector2(640, 150), 1f);

        var quitGo = Box("Button_quit", panel, new Vector3(0, -230, -40), new Vector3(640, 150, 60), mRed);
        var quitBtn = quitGo.AddComponent<WorldButton>();
        quitBtn.id = "quit";
        quitBtn.emissionColor = new Color(1f, 0.2f, 0.2f);
        Label("Label_quit", panel, new Vector3(0, -230, -75), Quaternion.identity,
            "ออกจากเกม", 80, Color.white, new Vector2(640, 150), 1f);

        AddText(panel, "Hint", new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.13f), 32,
            new Color(0.7f, 0.7f, 0.7f), TextAnchor.MiddleCenter).text =
            "VR: ชี้เลเซอร์ที่ปุ่มแล้วกด Trigger   |   PC: คลิกที่ปุ่ม";

        SetLayerRecursive(panel.gameObject, 5);
        panel.gameObject.SetActive(false);
        return panel.gameObject;
    }

    // ================================================================= handbook

    static GameObject BuildHandbook()
    {
        // 1300 x 1550 px at 0.0007 m/px = about 0.9 x 1.1 m, floating ~0.9 m in front of the player
        var panel = MakeCanvas("Handbook (Y)", null, new Vector3(0, 1.5f, 0), Quaternion.identity,
            new Vector2(1300, 1750), 0.0007f, new Color(0.04f, 0.05f, 0.07f, 0.95f));

        AddText(panel, "Title", new Vector2(0.04f, 0.93f), new Vector2(0.96f, 0.99f), 54,
            new Color(1f, 0.85f, 0.25f), TextAnchor.MiddleLeft).text =
            "คู่มือป้องกันรังสี  <size=30><color=#AAAAAA>(กด Y อีกครั้งเพื่อปิด)</color></size>";

        const string H = "<color=#FFD54A>";
        const string G = "<color=#5CFF8A>";
        const string R = "<color=#FF8A6A>";
        const string E = "</color>";

        AddText(panel, "Body", new Vector2(0.04f, 0.01f), new Vector2(0.96f, 0.92f), 26,
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
            "• เวลา: อยู่ในโซนนานเท่าไหร่ ก็ได้รับรังสีมากเท่านั้น → อย่าหยุดยืน รีบหนีรังสีที่ลามตามมาไปที่ประตู (กดสติ๊กซ้ายเพื่อวิ่ง)\n" +
            "• ระยะทาง: ยิ่งห่างจุดรั่ว รังสียิ่งลดลงเร็ว (แหล่งรังสีแบบจุด ห่างขึ้น 2 เท่า เหลือประมาณ 1/4)\n" +
            "• ที่กำบัง: ผนังหรือวัสดุหนาที่อยู่ระหว่างตัวเรากับแหล่งรังสีช่วยลดรังสีได้\n\n" +

            $"{H}5) Geiger Counter (ทุบตู้กระจกฉุกเฉินแล้วถือไว้){E}\n" +
            "ค่ารังสีดูได้จากเครื่องเท่านั้น: บรรทัดใหญ่ = อัตรารังสีตรงหัววัด (Sv/h)  DOSE = รังสีที่ร่างกายรับสะสม\n" +
            "เสียงคลิกยิ่งถี่ = ยิ่งใกล้จุดรั่ว  ถ้าขอบตาเริ่มมืด = ร่างกายรับรังสีมากแล้ว\n\n" +
            $"{H}6) ปริมาณรังสีกับผลต่อร่างกาย (รับทั้งตัวในครั้งเดียว){E}\n" +
            "<size=26>" + RadiationEffects.HandbookTable + "</size>\n\n" +
            "<size=22><color=#888888>* ระดับอาการอ้างอิงแนวทางของ CDC เรื่องกลุ่มอาการป่วยจากรังสีเฉียบพลัน (ARS); ความแรงของจุดรั่วและ % การป้องกันในเกมปรับเพื่อการสาธิต</color></size>";

        panel.gameObject.SetActive(false);
        return panel.gameObject;
    }

    // ================================================================= rig

    static ShowcaseRig BuildRig(Transform start, Grabbable[] items)
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

        return rig;
    }
    // ================================================================= geiger counter

    static GeigerCounter BuildGeigerCounter(RadiationSource rad)
    {
        var root = new GameObject("GeigerCounter (grab + hold)");
        var dev = root.transform;

        // body: probe points forward (+z), display on top (+y)
        var body = Box("Body", dev, Vector3.zero, new Vector3(0.085f, 0.055f, 0.15f), mYellow, false);
        Box("Grip", dev, new Vector3(0, -0.05f, -0.02f), new Vector3(0.035f, 0.05f, 0.07f), mDark, false);
        Box("Speaker", dev, new Vector3(0.0435f, 0, -0.03f), new Vector3(0.003f, 0.03f, 0.05f), mDark, false);
        var probe = Box("Probe", dev, new Vector3(0, 0, 0.125f), new Vector3(0.03f, 0.05f, 0.03f), mMetal, false, PrimitiveType.Cylinder);
        probe.transform.localRotation = Quaternion.Euler(90, 0, 0);
        Box("ProbeWindow", dev, new Vector3(0, 0, 0.175f), new Vector3(0.032f, 0.032f, 0.012f), mDark, false, PrimitiveType.Sphere);
        var tip = new GameObject("ProbeTip").transform;
        tip.SetParent(dev, false);
        tip.localPosition = new Vector3(0, 0, 0.18f);
        Box("Bezel", dev, new Vector3(0, 0.0285f, -0.005f), new Vector3(0.072f, 0.004f, 0.11f), mDark, false);

        // display: 640 x 1000 px -> 6.4 x 10 cm, lying on the top face, text "up" toward the probe
        var disp = MakeCanvas("Display", dev, new Vector3(0, 0.031f, -0.005f), Quaternion.Euler(90, 0, 0),
            new Vector2(640, 1000), 0.0001f, new Color(0.05f, 0.09f, 0.06f, 1f));

        AddText(disp, "Title", new Vector2(0.05f, 0.88f), new Vector2(0.8f, 0.98f), 60,
            new Color(1f, 0.85f, 0.3f), TextAnchor.MiddleLeft).text = "GEIGER";
        var alarm = AddText(disp, "Alarm", new Vector2(0.05f, 0.78f), new Vector2(0.95f, 0.87f), 56,
            new Color(1f, 0.25f, 0.2f), TextAnchor.MiddleCenter);
        alarm.text = "!! DANGER !!";
        alarm.enabled = false;

        var ledGo = new GameObject("LED", typeof(RectTransform), typeof(Image));
        var ledRt = (RectTransform)ledGo.transform;
        ledRt.SetParent(disp, false);
        ledRt.anchorMin = ledRt.anchorMax = new Vector2(0.88f, 0.93f);
        ledRt.sizeDelta = new Vector2(60, 60);
        var led = ledGo.GetComponent<Image>();

        // analog meter, log scale in Sv/h: 1µ / 1m / 1 / 1k
        var pivotPos = new Vector2(0f, -60f);
        string[] marks = { "1µ", "1m", "1", "1k" };
        float[] angles = { -49f, -16f, 16f, 49f };
        for (int i = 0; i < marks.Length; i++)
        {
            var m = new GameObject("Mark_" + i, typeof(RectTransform), typeof(Text));
            var mrt = (RectTransform)m.transform;
            mrt.SetParent(disp, false);
            mrt.sizeDelta = new Vector2(130, 70);
            float a = angles[i] * Mathf.Deg2Rad;
            mrt.anchoredPosition = pivotPos + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 300f;
            var mt = m.GetComponent<Text>();
            mt.font = editorFont; mt.fontSize = 48; mt.alignment = TextAnchor.MiddleCenter;
            mt.color = i >= 2 ? new Color(1f, 0.35f, 0.3f) : new Color(0.8f, 1f, 0.8f);
            mt.text = marks[i];
            texts.Add(mt);
        }
        var unit = AddText(disp, "Unit", new Vector2(0.35f, 0.40f), new Vector2(0.65f, 0.46f), 36,
            new Color(0.7f, 0.8f, 0.7f), TextAnchor.MiddleCenter);
        unit.text = "Sv/h";

        var needleGo = new GameObject("Needle", typeof(RectTransform), typeof(Image));
        var needle = (RectTransform)needleGo.transform;
        needle.SetParent(disp, false);
        needle.pivot = new Vector2(0.5f, 0f);
        needle.sizeDelta = new Vector2(10, 265);
        needle.anchoredPosition = pivotPos;
        needle.localRotation = Quaternion.Euler(0, 0, 60);
        needleGo.GetComponent<Image>().color = new Color(1f, 0.3f, 0.2f);

        var hub = new GameObject("NeedleHub", typeof(RectTransform), typeof(Image));
        var hubRt = (RectTransform)hub.transform;
        hubRt.SetParent(disp, false);
        hubRt.sizeDelta = new Vector2(40, 40);
        hubRt.anchoredPosition = pivotPos;
        hub.GetComponent<Image>().color = new Color(0.8f, 0.8f, 0.8f);

        var rateText = AddText(disp, "Rate", new Vector2(0.03f, 0.19f), new Vector2(0.97f, 0.36f), 86,
            Color.white, TextAnchor.MiddleCenter);
        rateText.text = "0 µSv/h";
        var doseText = AddText(disp, "Dose", new Vector2(0.03f, 0.03f), new Vector2(0.97f, 0.18f), 56,
            Color.white, TextAnchor.MiddleCenter);
        doseText.text = "DOSE 0 µSv";

        var col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0, -0.01f, 0.03f);
        col.size = new Vector3(0.09f, 0.09f, 0.22f);
        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 0.8f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.isKinematic = true;

        root.AddComponent<AudioSource>();
        var geiger = root.AddComponent<GeigerCounter>();
        geiger.gripPoint = Grip(dev, new Vector3(0, -0.06f, -0.02f), new Vector3(0, -0.45f, 1f), new Vector3(0, 1f, 0.45f));
        geiger.startKinematic = true;
        geiger.locked = true;
        geiger.source = rad;
        geiger.probeTip = tip;
        geiger.needle = needle;
        geiger.rateText = rateText;
        geiger.doseText = doseText;
        geiger.alarmText = alarm;
        geiger.led = led;
        geiger.glowRenderers = new[] { body.GetComponent<Renderer>() };
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
        main.playOnAwake = false;   // RadiationSource starts it when the leak begins
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

    static Volume BuildVolume()
    {
        string path = GenDir + "/ShowcaseVolumeProfile.asset";
        AssetDatabase.DeleteAsset(path);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);
        var bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(0.9f);
        bloom.threshold.Override(1f);
        var vig = profile.Add<Vignette>(true);
        vig.intensity.Override(0.2f);
        vig.color.Override(Color.black);
        vig.smoothness.Override(1f);
        var ca = profile.Add<ColorAdjustments>(true);
        ca.saturation.Override(0f);
        ca.postExposure.Override(0f);
        foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
        EditorUtility.SetDirty(profile);

        var go = new GameObject("Global Volume");
        var v = go.AddComponent<Volume>();
        v.isGlobal = true;
        v.sharedProfile = profile;
        return v;
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
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
        mAxeHandle = Lit("AxeHandle", new Color(0.75f, 0.08f, 0.06f), Color.black, 0.5f);
        mBeacon = Lit("BeaconRed", new Color(0.8f, 0.05f, 0.05f), Color.black, 0.85f);
        mCrate = Lit("Crate", new Color(0.45f, 0.33f, 0.2f), null, 0.15f);
        mGlass = TransparentLit("Glass", new Color(0.75f, 0.9f, 1f, 0.22f));
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

    static Material TransparentLit(string name, Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(shader);
        m.SetColor("_BaseColor", color);
        m.SetColor("_Color", color);
        m.SetFloat("_Smoothness", 0.95f);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
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

    /// <summary>Adds a grip point: hand position on the item; forward = hand pointing direction, up = back of the hand.</summary>
    static Transform Grip(Transform item, Vector3 localPos, Vector3 handForward, Vector3 handUp)
    {
        var g = new GameObject("GripPoint").transform;
        g.SetParent(item, false);
        g.localPosition = localPos;
        g.localRotation = Quaternion.LookRotation(handForward, handUp);
        return g;
    }

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
