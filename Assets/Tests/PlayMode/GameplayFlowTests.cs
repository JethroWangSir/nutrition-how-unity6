using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

public class GameplayFlowTests
{
    private const float SceneLoadTimeoutSeconds = 8f;
    private const string EffectsVolumePreference = "settings.effectsVolumeDb";
    private const string ReducedFlashingPreference = "settings.reducedFlashing";
    private string metricsTestPath;
    private bool hadEffectsVolumePreference;
    private float savedEffectsVolumePreference;
    private bool hadReducedFlashingPreference;
    private int savedReducedFlashingPreference;

    [SetUp]
    public void SetUp()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        metricsTestPath=Path.Combine(
            Application.temporaryCachePath,
            $"nutrition-how-playtest-metrics-{Guid.NewGuid():N}.jsonl");
        InvokeStatic("GameplayPlaytestMetrics","ConfigureOutputForTests",metricsTestPath);
        hadEffectsVolumePreference=PlayerPrefs.HasKey(EffectsVolumePreference);
        savedEffectsVolumePreference=PlayerPrefs.GetFloat(
            EffectsVolumePreference,
            0f);
        hadReducedFlashingPreference=PlayerPrefs.HasKey(ReducedFlashingPreference);
        savedReducedFlashingPreference=PlayerPrefs.GetInt(
            ReducedFlashingPreference,
            0);
        ResetProgress();
    }

    [TearDown]
    public void TearDown()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if(!string.IsNullOrEmpty(metricsTestPath) && File.Exists(metricsTestPath))
        {
            File.Delete(metricsTestPath);
        }
        ResetProgress();
        RestorePreference(
            EffectsVolumePreference,
            hadEffectsVolumePreference,
            savedEffectsVolumePreference);
        RestorePreference(
            ReducedFlashingPreference,
            hadReducedFlashingPreference,
            savedReducedFlashingPreference);
        PlayerPrefs.Save();
    }

    private static void ResetProgress()
    {
        Time.timeScale = 1f;
        Type progressType = FindRuntimeType("pass");
        if (progressType != null)
        {
            MethodInfo clearProgress = progressType.GetMethod(
                "ClearProgress",
                BindingFlags.Static | BindingFlags.Public);
            if(clearProgress != null)
            {
                clearProgress.Invoke(null,null);
            }else
            {
                SetProgress(progressType, false);
            }
        }
    }

    [UnityTest]
    [Order(1)]
    public IEnumerator StartAndOpeningFlowReachesMenu()
    {
        yield return LoadScene(NutritionHowScenes.Start);
        MonoBehaviour startMenu = RequireBehaviour("menu", "start menu");
        SetTransitionTimeToZero();

        Invoke(startMenu, "StartGame");
        yield return WaitForScene(NutritionHowScenes.Opening, "opening");

        MonoBehaviour openingMenu = RequireBehaviour("menu", "opening menu");
        SetTransitionTimeToZero();

        Invoke(openingMenu, "Skip");
        yield return WaitForScene(NutritionHowScenes.Menu, "menu");
    }

    [UnityTest]
    [Order(2)]
    public IEnumerator MenuButtonsReachEveryLevelInstruction()
    {
        string[] routes = { "Level1", "Level2", "Level3", "Level4" };
        int[] expectedSceneIndexes =
        {
            NutritionHowScenes.Instruction1,
            NutritionHowScenes.Instruction2,
            NutritionHowScenes.Instruction3Part1,
            NutritionHowScenes.Instruction4
        };

        for (int index = 0; index < routes.Length; index++)
        {
            yield return LoadScene(NutritionHowScenes.Menu);
            MonoBehaviour menuController = RequireBehaviour("menu", "main menu");
            SetTransitionTimeToZero();

            Invoke(menuController, routes[index]);
            yield return WaitForScene(
                expectedSceneIndexes[index],
                $"level {index + 1} instruction");
        }
    }

    [UnityTest]
    [Order(3)]
    public IEnumerator CompletingAllLevelsLoadsEndingAndClearsProgress()
    {
        yield return LoadScene(NutritionHowScenes.Menu);
        foreach (GameObject rootObject in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            rootObject.SetActive(false);
        }

        GameObject loaderObject = new GameObject("levelLoader");
        MonoBehaviour loader = AddRuntimeComponent(loaderObject, "levelLoader");
        SetFloatField(loader, "transitionTime", 0f);

        GameObject progressObject = new GameObject("pass");
        MonoBehaviour progress = AddRuntimeComponent(progressObject, "pass");
        ReplaceProgressAnimations(progress);
        SetFloatField(progress, "endingDelay", 0f);

        SetProgress(progress.GetType(), true);
        Invoke(progress, "EvaluateCompletion");

        yield return WaitForScene(NutritionHowScenes.Ending, "ending");

        AssertProgressIsCleared(progress.GetType());
    }

    [UnityTest]
    [Order(4)]
    public IEnumerator PauseResumeAndHomeRestoreTimeAndReachMenu()
    {
        yield return LoadScene(NutritionHowScenes.Level1);
        MonoBehaviour menuController = RequireBehaviour("menu", "level menu");

        Invoke(menuController, "Pause");
        Assert.AreEqual(0f, Time.timeScale);

        Invoke(menuController, "Resume");
        Assert.AreEqual(1f, Time.timeScale);

        SetTransitionTimeToZero();
        Invoke(menuController, "Pause");
        Invoke(menuController, "Home");

        yield return WaitForScene(NutritionHowScenes.Menu, "menu after pause");
        Assert.AreEqual(1f, Time.timeScale);
    }

    [UnityTest]
    [Order(5)]
    public IEnumerator DirectSceneLoadWhilePausedRestoresTimeAndCompletesTransition()
    {
        yield return LoadScene(NutritionHowScenes.Level1);
        MonoBehaviour loader = RequireBehaviour("levelLoader", "level loader");
        SetFloatField(loader, "transitionTime", 0.05f);

        Time.timeScale = 0f;
        Invoke(loader, "LoadSomeLevel", NutritionHowScenes.Menu);

        yield return WaitForScene(NutritionHowScenes.Menu, "menu requested while paused");
        Assert.AreEqual(1f, Time.timeScale);
    }

    [UnityTest]
    [Order(6)]
    public IEnumerator DeathDialogAppearsAndRestartReloadsTheLevel()
    {
        yield return LoadScene(NutritionHowScenes.Level1);
        MonoBehaviour activePlayer = RequireBehaviour("player", "level 1 player");
        GameObject loseDialog = (GameObject)GetField(activePlayer, "loseDialog");

        SetField(activePlayer, "hp", 0);
        Invoke(activePlayer, "Hpfunction");
        yield return new WaitForSecondsRealtime(1.6f);

        Assert.IsTrue(loseDialog.activeSelf, "The death dialog did not appear.");
        Assert.IsTrue((bool)GetField(activePlayer, "failureFeedbackPlayed"),
            "The death flow did not issue its one-shot failure feedback.");
        Assert.AreEqual(0f, Time.timeScale, "Gameplay continued behind the failure dialog.");

        MonoBehaviour menuController = RequireBehaviour("menu", "level menu");
        SetTransitionTimeToZero();
        int previousSceneHandle = SceneManager.GetActiveScene().handle;
        Invoke(menuController, "Restart");

        yield return WaitForSceneReload(previousSceneHandle, NutritionHowScenes.Level1);

        MonoBehaviour restartedPlayer = RequireBehaviour("player", "restarted level 1 player");
        GameObject restartedLoseDialog = (GameObject)GetField(restartedPlayer, "loseDialog");
        Assert.IsFalse(restartedLoseDialog.activeSelf, "The death dialog remained open after restart.");
        Assert.AreEqual(1f, Time.timeScale);
    }

    [UnityTest]
    [Order(7)]
    public IEnumerator BossDefeatOpensTheExitOnce()
    {
        yield return LoadScene(NutritionHowScenes.Level1);
        MonoBehaviour boss = RequireBehaviour("caker", "level 1 boss");
        GameObject exitDoor = (GameObject)GetField(boss, "door");

        SetField(boss, "hp", 0);
        Invoke(boss, "Hpfunction");
        yield return new WaitForSecondsRealtime(1.1f);

        Assert.IsTrue(exitDoor.activeSelf, "The exit did not open after the boss was defeated.");
        Assert.IsTrue((bool)GetField(boss, "deathScheduled"));
    }

    [UnityTest]
    [Order(8)]
    public IEnumerator ThirdLevelShipRetainsUsableFlightAndDamageValues()
    {
        yield return LoadScene(NutritionHowScenes.Level3Part2);
        MonoBehaviour levelShip = RequireBehaviour("ship", "level 3 ship");

        Assert.Greater((float)GetField(levelShip, "jumpForce"), 0f);
        Assert.Greater((float)GetField(levelShip, "hurtForce"), 0f);
        Assert.Greater((float)GetField(levelShip, "speed"), 0f);
        Assert.Greater((int)GetField(levelShip, "max_hp"), 0);

        GameObject exitDoor = (GameObject)GetField(levelShip, "door");
        exitDoor.SetActive(false);
        SetField(levelShip, "collections", 8);
        Invoke(levelShip, "Collectionsnum");

        Assert.IsTrue(exitDoor.activeSelf, "The collection milestone did not unlock the exit.");
        Assert.IsTrue((bool)GetField(levelShip, "winAnimationPlayed"),
            "The collection milestone did not record its one-shot completion feedback.");
    }

    [UnityTest]
    [Order(9)]
    public IEnumerator FourthLevelTimeoutShowsLoseDialogAndStopsWaterTrigger()
    {
        yield return LoadScene(NutritionHowScenes.Level4Part1);
        MonoBehaviour levelShip = RequireBehaviour("ship4", "level 4 ship");
        GameObject loseDialog = (GameObject)GetField(levelShip, "loseDialog");
        GameObject water = (GameObject)GetField(levelShip, "water");

        SetField(levelShip, "timeLeft", 5.1f);
        Invoke(levelShip, "Timer");
        Assert.AreEqual(4, (int)GetField(levelShip, "countdownImageIndex"));

        SetField(levelShip, "timeLeft", 4.1f);
        Invoke(levelShip, "Timer");
        Assert.AreEqual(3, (int)GetField(levelShip, "countdownImageIndex"));

        SetField(levelShip, "timeLeft", 0f);
        Invoke(levelShip, "Timer");

        Assert.IsTrue(loseDialog.activeSelf, "The timeout dialog did not appear.");
        Assert.IsTrue((bool)GetField(levelShip, "timerExpired"));
        Assert.IsTrue((bool)GetField(levelShip, "failureFeedbackPlayed"),
            "The timeout flow did not issue its one-shot failure feedback.");
        Assert.IsFalse(water.GetComponent<BoxCollider2D>().enabled);
        Assert.AreEqual(0f, Time.timeScale, "Gameplay continued behind the timeout dialog.");
    }

    [UnityTest]
    [Order(10)]
    public IEnumerator PlatformJumpBuffersInputUntilLanding()
    {
        yield return LoadScene(NutritionHowScenes.Level1);
        MonoBehaviour activePlayer = RequireBehaviour("player", "level 1 player");
        activePlayer.enabled = false;
        Rigidbody2D body = activePlayer.GetComponent<Rigidbody2D>();
        body.linearVelocity = Vector2.zero;

        SetField(activePlayer, "isGround", false);
        Invoke(activePlayer, "QueueJump");
        Invoke(activePlayer, "UpdateJumpGraceTimers", 0.02f);
        Assert.Greater((float)GetField(activePlayer, "jumpBufferTimer"), 0f);

        SetField(activePlayer, "isGround", true);
        Invoke(activePlayer, "UpdateJumpGraceTimers", 0.02f);
        Invoke(activePlayer, "TryBufferedJump");

        Assert.Greater(body.linearVelocity.y, 0f,
            "A jump pressed just before landing was not performed on landing.");
        Assert.AreEqual(0f, (float)GetField(activePlayer, "jumpBufferTimer"));
    }

    [UnityTest]
    [Order(11)]
    public IEnumerator PlatformJumpAllowsBriefCoyoteTimeAfterLeavingGround()
    {
        yield return LoadScene(NutritionHowScenes.Level1);
        MonoBehaviour activePlayer = RequireBehaviour("player", "level 1 player");
        activePlayer.enabled = false;
        Rigidbody2D body = activePlayer.GetComponent<Rigidbody2D>();
        body.linearVelocity = Vector2.zero;

        SetField(activePlayer, "isGround", true);
        Invoke(activePlayer, "UpdateJumpGraceTimers", 0.02f);
        SetField(activePlayer, "isGround", false);
        Invoke(activePlayer, "UpdateJumpGraceTimers", 0.04f);
        Invoke(activePlayer, "QueueJump");
        Invoke(activePlayer, "TryBufferedJump");

        Assert.Greater(body.linearVelocity.y, 0f,
            "A jump pressed just after leaving a platform was not accepted.");
        Assert.AreEqual(0f, (float)GetField(activePlayer, "coyoteTimer"));
    }

    [UnityTest]
    [Order(12)]
    public IEnumerator GameplayCamerasUseModeSpecificMotionComfortZones()
    {
        int[] sceneIndexes =
        {
            NutritionHowScenes.Level1,
            NutritionHowScenes.Level2,
            NutritionHowScenes.Level3Part1,
            NutritionHowScenes.Level3Part2,
            NutritionHowScenes.Level4Part1,
            NutritionHowScenes.Level4Part2
        };
        Vector2[] expectedDeadZones =
        {
            new Vector2(0.08f,0.04f),
            new Vector2(0.08f,0.04f),
            new Vector2(0.08f,0.04f),
            new Vector2(0.04f,0.08f),
            new Vector2(0.08f,0.08f),
            new Vector2(0.08f,0.08f)
        };
        Type framingType=FindRuntimeType("Cinemachine.CinemachineFramingTransposer");
        Assert.IsNotNull(framingType,"Cinemachine framing type is unavailable.");

        for(int index=0;index<sceneIndexes.Length;index++)
        {
            yield return LoadScene(sceneIndexes[index]);
            Scene activeScene=SceneManager.GetActiveScene();
            Component framing=Resources.FindObjectsOfTypeAll(framingType)
                .OfType<Component>()
                .FirstOrDefault(candidate => candidate.gameObject.scene==activeScene);
            Assert.IsNotNull(framing,$"No gameplay camera framing component in {activeScene.name}.");

            Assert.That(GetFloatField(framing,"m_DeadZoneWidth"),
                Is.EqualTo(expectedDeadZones[index].x).Within(0.001f),
                $"Unexpected horizontal camera zone in {activeScene.name}.");
            Assert.That(GetFloatField(framing,"m_DeadZoneHeight"),
                Is.EqualTo(expectedDeadZones[index].y).Within(0.001f),
                $"Unexpected vertical camera zone in {activeScene.name}.");
            Assert.That(GetFloatField(framing,"m_LookaheadTime"),Is.EqualTo(0f).Within(0.001f),
                $"Unvalidated lookahead was enabled in {activeScene.name}.");
        }
    }

    [UnityTest]
    [Order(13)]
    public IEnumerator LocalPlaytestMetricsRecordOneOutcomePerAttempt()
    {
        yield return LoadScene(NutritionHowScenes.Level4Part1);
        if(File.Exists(metricsTestPath))
        {
            File.Delete(metricsTestPath);
        }

        InvokeStatic("GameplayPlaytestMetrics","ConfigureOutputForTests",metricsTestPath);
        InvokeStatic("GameplayPlaytestMetrics","BeginLevelForTests",4);
        InvokeStatic("GameplayPlaytestMetrics","FailActiveLevel","time_expired");
        InvokeStatic("GameplayPlaytestMetrics","FailActiveLevel","time_expired");

        Assert.IsTrue(File.Exists(metricsTestPath),"The local playtest metrics file was not created.");
        string[] records=File.ReadAllLines(metricsTestPath);
        Assert.AreEqual(1,records.Length,"One failed attempt produced duplicate metric records.");
        StringAssert.Contains("\"level\":4",records[0]);
        StringAssert.Contains("\"attempt\":1",records[0]);
        StringAssert.Contains("\"outcome\":\"failed\"",records[0]);
        StringAssert.Contains("\"reason\":\"time_expired\"",records[0]);

        InvokeStatic("GameplayPlaytestMetrics","BeginLevelForTests",4);
        InvokeStatic("GameplayPlaytestMetrics","CompleteLevel",4);
        records=File.ReadAllLines(metricsTestPath);
        Assert.AreEqual(2,records.Length);
        StringAssert.Contains("\"attempt\":2",records[1]);
        StringAssert.Contains("\"outcome\":\"completed\"",records[1]);
        StringAssert.Contains("\"elapsedSeconds\":",records[1]);
    }

    [UnityTest]
    [Order(14)]
    public IEnumerator AccessibilityControlsPersistAndReduceDamageFlashing()
    {
        InvokeStatic("GameAccessibilitySettings","SetEffectsVolume",-20f);
        InvokeStatic("GameAccessibilitySettings","SetReducedFlashing",true);
        ResetProgress();

        Assert.That((float)GetStaticProperty("GameAccessibilitySettings","EffectsVolumeDb"),
            Is.EqualTo(-20f).Within(0.001f));
        Assert.That((float)GetStaticProperty("GameAccessibilitySettings","EffectsVolumeLinear"),
            Is.EqualTo(0.1f).Within(0.001f));
        Assert.IsTrue((bool)GetStaticProperty("GameAccessibilitySettings","ReducedFlashingEnabled"),
            "Campaign progress reset removed the accessibility setting.");

        yield return LoadScene(NutritionHowScenes.Start);
        Slider effectsSlider=GameObject.Find("EffectsVolumeSlider")?.GetComponent<Slider>();
        Assert.IsNotNull(effectsSlider,"The independent effects-volume control is missing.");
        Assert.That(effectsSlider.value,Is.EqualTo(-20f).Within(0.001f));

        Button flashingButton=GameObject.Find("ReducedFlashingButton")?.GetComponent<Button>();
        Assert.IsNotNull(flashingButton,"The reduced-flashing control is missing.");
        StringAssert.Contains("開",flashingButton.GetComponentInChildren<Text>().text);
        flashingButton.onClick.Invoke();
        Assert.IsFalse((bool)GetStaticProperty("GameAccessibilitySettings","ReducedFlashingEnabled"));
        StringAssert.Contains("關",flashingButton.GetComponentInChildren<Text>().text);
        flashingButton.onClick.Invoke();
        Assert.IsTrue((bool)GetStaticProperty("GameAccessibilitySettings","ReducedFlashingEnabled"));
        StringAssert.Contains("開",flashingButton.GetComponentInChildren<Text>().text);

        GameObject feedbackTarget=new GameObject("damage-feedback-test");
        SpriteRenderer renderer=feedbackTarget.AddComponent<SpriteRenderer>();
        renderer.color=new Color(1f,1f,1f,0.8f);
        Type feedbackType=FindRuntimeType("SpriteDamageFeedback");
        Assert.IsNotNull(feedbackType,"The damage-feedback runtime type is missing.");
        object feedback=Activator.CreateInstance(feedbackType,new object[] { feedbackTarget });
        InvokeObject(feedback,"Pulse",0f);
        float firstAlpha=renderer.color.a;
        float pulsePeriod=(float)feedbackType.GetField("PulsePeriod").GetRawConstantValue();
        float reducedAlpha=(float)feedbackType.GetField("ReducedFlashingAlpha").GetRawConstantValue();
        InvokeObject(feedback,"Pulse",pulsePeriod*0.5f);
        float secondAlpha=renderer.color.a;

        Assert.That(firstAlpha,Is.EqualTo(0.8f*reducedAlpha).Within(0.001f));
        Assert.That(secondAlpha,Is.EqualTo(firstAlpha).Within(0.001f),
            "Reduced flashing still produced a rapid alpha pulse.");

        InvokeObject(feedback,"Restore");
        Assert.That(renderer.color.a,Is.EqualTo(0.8f).Within(0.001f));
        UnityEngine.Object.Destroy(feedbackTarget);
        yield return null;
    }

    [UnityTest]
    [Order(15)]
    public IEnumerator MouseCursorIsCompactAccurateAndGamepadCursorStartsHidden()
    {
        yield return LoadScene(NutritionHowScenes.Start);
        MonoBehaviour mouseCursor=RequireBehaviour("cursorController","mouse cursor controller");
        Texture2D normalCursor=(Texture2D)GetField(mouseCursor,"cursor");
        Texture2D clickedCursor=(Texture2D)GetField(mouseCursor,"cursorClicked");

        Assert.LessOrEqual(normalCursor.width,64,"The normal cursor is still oversized.");
        Assert.LessOrEqual(normalCursor.height,64,"The normal cursor is still oversized.");
        Assert.LessOrEqual(clickedCursor.width,64,"The clicked cursor is still oversized.");
        Assert.LessOrEqual(clickedCursor.height,64,"The clicked cursor is still oversized.");

        Vector2 normalHotspot=(Vector2)InvokeStaticResult(
            "cursorController","GetHotspot",normalCursor,false);
        Vector2 clickedHotspot=(Vector2)InvokeStaticResult(
            "cursorController","GetHotspot",clickedCursor,true);
        Assert.That(normalHotspot.x,Is.EqualTo(Mathf.Round(normalCursor.width*0.28f)));
        Assert.That(normalHotspot.y,Is.EqualTo(Mathf.Round(normalCursor.height*0.18f)));
        Assert.That(clickedHotspot.x,Is.EqualTo(Mathf.Round(clickedCursor.width*0.20f)));
        Assert.That(clickedHotspot.y,Is.EqualTo(Mathf.Round(clickedCursor.height*0.20f)));
        Assert.AreNotEqual(Vector2.zero,normalHotspot,
            "The cursor hotspot still incorrectly uses the texture corner.");

        yield return LoadScene(NutritionHowScenes.Level1);
        MonoBehaviour gamepadController=RequireBehaviour(
            "gamepadCursorController","gamepad cursor controller");
        RectTransform gamepadCursor=(RectTransform)GetField(gamepadController,"cursorTransform");
        Assert.IsFalse(gamepadCursor.gameObject.activeSelf,
            "The gamepad cursor appeared before any gamepad cursor input.");
        Assert.IsTrue(Cursor.visible,"The system mouse cursor was hidden in mouse mode.");

        Assert.IsFalse((bool)InvokeStaticResult(
            "gamepadCursorController","ShouldActivateGamepadCursor",
            "Keyboard&Mouse",Vector2.one,true),
            "Gamepad cursor activation ignored the active control scheme.");
        Assert.IsFalse((bool)InvokeStaticResult(
            "gamepadCursorController","ShouldActivateGamepadCursor",
            "Gamepad",new Vector2(0.1f,0.1f),false),
            "Minor gamepad stick drift activated the virtual cursor.");
        Assert.IsTrue((bool)InvokeStaticResult(
            "gamepadCursorController","ShouldActivateGamepadCursor",
            "Gamepad",new Vector2(0.5f,0f),false),
            "Intentional gamepad cursor movement did not activate the virtual cursor.");
        Assert.IsTrue((bool)InvokeStaticResult(
            "gamepadCursorController","ShouldActivateGamepadCursor",
            "Gamepad",Vector2.zero,true),
            "A fresh gamepad submit press did not activate the virtual cursor.");

        AssertSprintCooldownIconInHud();
        yield return LoadScene(NutritionHowScenes.Level2);
        AssertSprintCooldownIconInHud();
        yield return LoadScene(NutritionHowScenes.Level3Part1);
        AssertSprintCooldownIconInHud();
    }

    private static void AssertSprintCooldownIconInHud()
    {
        GameObject iconObject=GameObject.Find("sprintIcon");
        Assert.IsNotNull(iconObject,
            $"The sprint cooldown icon is missing in {SceneManager.GetActiveScene().name}.");
        RectTransform icon=iconObject.GetComponent<RectTransform>();
        Assert.IsNotNull(icon);
        Assert.That(icon.anchoredPosition.x,Is.EqualTo(125f).Within(0.001f));
        Assert.That(icon.anchoredPosition.y,Is.EqualTo(-70f).Within(0.001f),
            "The cooldown icon drifted out of the upper-left HUD panel.");
        Assert.That(icon.position.x,Is.GreaterThan(0f));
        Assert.That(icon.position.x,Is.LessThan(Screen.width*0.35f),
            "The cooldown icon is no longer in the left HUD region.");
        Assert.That(icon.position.y,Is.GreaterThan(Screen.height*0.75f),
            "The cooldown icon is floating in the gameplay area instead of the upper HUD.");
        Assert.That(icon.position.y,Is.LessThan(Screen.height));

        Transform cooldownFill=icon.Find("cdImage");
        Assert.IsNotNull(cooldownFill,"The sprint cooldown fill overlay is missing.");
        Assert.IsTrue(cooldownFill.gameObject.activeInHierarchy);
    }

    private static void RestorePreference(string key,bool hadValue,float value)
    {
        if(hadValue)
        {
            PlayerPrefs.SetFloat(key,value);
        }else
        {
            PlayerPrefs.DeleteKey(key);
        }
    }

    private static void RestorePreference(string key,bool hadValue,int value)
    {
        if(hadValue)
        {
            PlayerPrefs.SetInt(key,value);
        }else
        {
            PlayerPrefs.DeleteKey(key);
        }
    }

    private static IEnumerator LoadScene(int buildIndex)
    {
        ResetProgress();
        AsyncOperation load = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single);
        Assert.IsNotNull(load, $"Could not begin loading scene {buildIndex}.");

        while (!load.isDone)
        {
            yield return null;
        }

        DisableEventSystems();
        ResetProgress();

        yield return null;
        Assert.AreEqual(buildIndex, SceneManager.GetActiveScene().buildIndex);
    }

    private static IEnumerator WaitForScene(int buildIndex, string description)
    {
        float deadline = Time.realtimeSinceStartup + SceneLoadTimeoutSeconds;
        while (SceneManager.GetActiveScene().buildIndex != buildIndex &&
               Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        Assert.AreEqual(
            buildIndex,
            SceneManager.GetActiveScene().buildIndex,
            $"Timed out while loading {description}.");

        DisableEventSystems();
        // The level-loader prefab uses a 0.3 second transition. Allow any
        // already-started transition and scene-unload callbacks to finish
        // before the test starts another transition.
        yield return new WaitForSecondsRealtime(0.5f);
    }

    private static IEnumerator WaitForSceneReload(int previousHandle, int expectedBuildIndex)
    {
        float deadline = Time.realtimeSinceStartup + SceneLoadTimeoutSeconds;
        while (SceneManager.GetActiveScene().handle == previousHandle &&
               Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        Assert.AreNotEqual(
            previousHandle,
            SceneManager.GetActiveScene().handle,
            "Timed out while reloading the active scene.");
        Assert.AreEqual(expectedBuildIndex, SceneManager.GetActiveScene().buildIndex);
        DisableEventSystems();
        yield return new WaitForSecondsRealtime(0.5f);
    }

    private static void DisableEventSystems()
    {
        foreach (EventSystem eventSystem in UnityEngine.Object.FindObjectsByType<EventSystem>(
                     FindObjectsInactive.Exclude,
                     FindObjectsSortMode.None))
        {
            eventSystem.enabled = false;
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        DisableEventSystems();
        ResetProgress();
    }

    private static MonoBehaviour RequireBehaviour(string typeName, string description)
    {
        MonoBehaviour behaviour = UnityEngine.Object
            .FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate.GetType().Name == typeName);
        Assert.IsNotNull(behaviour, $"Could not find {description} in the active scene.");
        return behaviour;
    }

    private static void SetTransitionTimeToZero()
    {
        MonoBehaviour loader = RequireBehaviour("levelLoader", "level loader");
        SetFloatField(loader, "transitionTime", 0f);
    }

    private static void Invoke(MonoBehaviour target, string methodName, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(method, $"{target.GetType().Name}.{methodName} is missing.");
        method.Invoke(target, arguments);
    }

    private static Type FindRuntimeType(string typeName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(typeName))
            .FirstOrDefault(type => type != null);
    }

    private static void InvokeStatic(string typeName,string methodName,params object[] arguments)
    {
        Type type=FindRuntimeType(typeName);
        Assert.IsNotNull(type,$"Could not find runtime type {typeName}.");
        MethodInfo method=type.GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(method,$"{typeName}.{methodName} is missing.");
        method.Invoke(null,arguments);
    }

    private static object InvokeStaticResult(
        string typeName,string methodName,params object[] arguments)
    {
        Type type=FindRuntimeType(typeName);
        Assert.IsNotNull(type,$"Could not find runtime type {typeName}.");
        MethodInfo method=type.GetMethod(
            methodName,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(method,$"{typeName}.{methodName} is missing.");
        return method.Invoke(null,arguments);
    }

    private static object GetStaticProperty(string typeName,string propertyName)
    {
        Type type=FindRuntimeType(typeName);
        Assert.IsNotNull(type,$"Could not find runtime type {typeName}.");
        PropertyInfo property=type.GetProperty(
            propertyName,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(property,$"{typeName}.{propertyName} is missing.");
        return property.GetValue(null);
    }

    private static void InvokeObject(object target,string methodName,params object[] arguments)
    {
        MethodInfo method=target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(method,$"{target.GetType().Name}.{methodName} is missing.");
        method.Invoke(target,arguments);
    }

    private static MonoBehaviour AddRuntimeComponent(GameObject target, string typeName)
    {
        Type type = FindRuntimeType(typeName);
        Assert.IsNotNull(type, $"Could not find runtime type {typeName}.");
        return (MonoBehaviour)target.AddComponent(type);
    }

    private static void SetFloatField(MonoBehaviour target, string fieldName, float value)
    {
        FieldInfo field = target.GetType().GetField(fieldName);
        Assert.IsNotNull(field, $"{target.GetType().Name}.{fieldName} is missing.");
        field.SetValue(target, value);
    }

    private static float GetFloatField(Component target, string fieldName)
    {
        FieldInfo field=target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(field,$"{target.GetType().Name}.{fieldName} is missing.");
        return (float)field.GetValue(target);
    }

    private static object GetField(MonoBehaviour target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"{target.GetType().Name}.{fieldName} is missing.");
        return field.GetValue(target);
    }

    private static void SetField(MonoBehaviour target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"{target.GetType().Name}.{fieldName} is missing.");
        field.SetValue(target, value);
    }

    private static void SetProgress(Type progressType, bool value)
    {
        foreach (string fieldName in new[]
                 { "level1Pass", "level2Pass", "level3Pass", "level4Pass" })
        {
            FieldInfo field = progressType.GetField(fieldName);
            Assert.IsNotNull(field, $"pass.{fieldName} is missing.");
            field.SetValue(null, value);
        }
    }

    private static void ReplaceProgressAnimations(MonoBehaviour progress)
    {
        FieldInfo field = progress.GetType().GetField("levelPassAnim");
        Assert.IsNotNull(field, "pass.levelPassAnim is missing.");

        GameObject[] inertIndicators =
        {
            new GameObject("TestLevel1Complete"),
            new GameObject("TestLevel2Complete"),
            new GameObject("TestLevel3Complete"),
            new GameObject("TestLevel4Complete")
        };
        field.SetValue(progress, inertIndicators);
    }

    private static void AssertProgressIsCleared(Type progressType)
    {
        foreach (string fieldName in new[]
                 { "level1Pass", "level2Pass", "level3Pass", "level4Pass" })
        {
            FieldInfo field = progressType.GetField(fieldName);
            Assert.IsNotNull(field, $"pass.{fieldName} is missing.");
            Assert.IsFalse((bool)field.GetValue(null), $"pass.{fieldName} was not cleared.");
        }
    }
}
