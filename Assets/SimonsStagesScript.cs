using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SimonsStagesScript : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMBossModule BossModule;
    public KMBombModule Module;

    public LightInformation[] lightDevices;
    public IndicatorInformation[] indicatorLights;
    public TextMesh indicatorText;
    public Color[] lightDeviceColorOptions;
    public Material[] lightBaseOptions;
    public string[] lightTextOptions;
    public string[] lightNameOptions;
    public AudioClip[] sfxOptions;
    private List<int> chosenIndices = new List<int>();
    private List<int> chosenIndices2 = new List<int>();

    public TextMesh counterText;
    public int currentLevel = 0;

    public static readonly string[] defaultIgnoredModules =
    {
       "Simon's Stages",   //Mandatory to prevent unsolvable bombs.
       "Forget Me Not",     //Mandatory to prevent unsolvable bombs.
       "Forget Everything", //Cruel FMN.
       "Turn The Key",      //TTK is timer based, and stalls the bomb if only it and FMN are left.
       "Souvenir",          //Similar situation to TTK, stalls the bomb.
       "The Time Keeper",   //Again, similar to TTK.
       "The Swan",   //Again, similar to TTK.
       "Forget This", //Mandatory to prevent potentially unsolvable bombs.
     };
    public string[] ignoredModules;

    public int moduleCount = 0;
    public int solvedModules = 0;
    //private int tempSolvedModules = 0;

    public List<int> sequences = new List<int>();
    public List<string> solutionNames = new List<string>();
    public List<int> sequenceLengths = new List<int>();
    public List<int> solveLengths = new List<int>();
    public List<int> startLocation = new List<int>();
    public List<int> solutionStartLocation = new List<int>();
    private int lastStartPosition = 0;
    private int lastSolutionLocation = 0;
    public List<int> currentSequence = new List<int>();
    public List<string> currentSequenceNames = new List<string>();
    public List<int> currentSolution = new List<int>();
    public List<string> currentSolutionNames = new List<string>();
    public List<int> indicatorColour = new List<int>();
    public List<string> indicatorLetter = new List<string>();
    private int indicator = 0;
    string result = "";

    public List<int> repeatedSequence = new List<int>();
    public List<int> incorrectSequenceLength = new List<int>();
    public List<int> incorrectIndicatorLights = new List<int>();
    public List<string> incorrectIndicatorLetter = new List<string>();
    public List<int> incorrectSequenceStartLocation = new List<int>();
    public List<string> tempCorrectSolution = new List<string>();
    public List<int> incorrectSolutionStartLocation = new List<int>();
    public List<int> incorrectSolveLengths = new List<int>();
    public List<int> tempCurrent = new List<int>();
    public List<int> clearLights = new List<int>();
    public List<int> completeLights = new List<int>();
    public List<int> absoluteLevelPosition = new List<int>();
    public List<int> tempAbsolute = new List<int>();

    private int totalPresses = 0;
    private int stagePresses = 0;
    public int increaser = 0;
    private int stageIncreaser = 0;
    public List<bool> lightsSolved = new List<bool>();
    public List<bool> stagesSolved = new List<bool>();

    //Logging
    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved = false;
    private bool moduleLocked = true;
    private bool reverse = false;
    private bool gameOn = false;
    private bool readyToSolve = false;
    private bool secondAttempt;
    private bool secondAttemptLock;
    private bool checking;

    void Awake()
    {
        moduleId = moduleIdCounter++;
        ignoredModules = BossModule.GetIgnoredModules(Module, defaultIgnoredModules);
        foreach (LightInformation button in lightDevices)
        {
            LightInformation pressedButton = button;
            button.connectedButton.OnInteract += delegate () { ButtonPress(pressedButton); return false; };
        }
    }
    bool flashingCurStage = false; // Currently used to allow the flashes to finish on each stage of Simon's Stages.
    int stagesToGenerate = 0;
    void Update()
    {
        if (!moduleLocked && !moduleSolved && gameOn && !readyToSolve && !secondAttempt && !flashingCurStage)
        {
            solvedModules = Bomb.GetSolvedModuleNames().Where(x => !ignoredModules.Contains(x)).Count();
            if (moduleCount == 0 && !moduleSolved)
            {
                moduleSolved = true;
                GetComponent<KMBombModule>().HandlePass();
                Debug.LogFormat("[Simon's Stages #{0}] There are no solveable modules on the bomb. Module disarmed.", moduleId);
                StartCoroutine(SolveLights());
            }
            else if (moduleCount != 1 && currentLevel < stagesToGenerate)  // Replaced (solvedModules != moduleCount)
            {
                if (currentLevel < solvedModules)// Replaced (solvedModules != moduleCount)
                {
                    GenerateSequence();
                }
            }
            else
            {
                currentLevel++;
                counterText.text = "";
                indicatorText.text = "";
                Audio.PlaySoundAtTransform("scaryRiff", transform);
                Debug.LogFormat("[Simon's Stages #{0}] There are no more solveable modules on the bomb. The module is ready to solve.", moduleId);
                if (sequences.Count != 0)
                {
                    Debug.LogFormat("[Simon's Stages #{0}] STAGE 1 RESPONSE:", moduleId);
                }
                readyToSolve = true;
                for (int i = 0; i <= 9; i++)
                {
                    lightDevices[i].ledGlow.enabled = false;
                    lightDevices[i].greyBase.enabled = true;
                    indicatorLights[i].glow.enabled = false;
                }
                if (solutionNames.Count == 0)
                {
                    moduleSolved = true;
                    GetComponent<KMBombModule>().HandlePass();
                    Debug.LogFormat("[Simon's Stages #{0}] No sequences have been generated. Module disarmed.", moduleId);
                    StartCoroutine(SolveLights());
                }
                else
                {
                    StartCoroutine(IndicatorBlink());
                }
            }
        }
    }

    void GenerateSequence()
    {
        absoluteLevelPosition.Add(absoluteLevelPosition.Last() + 1);
        currentSequence.Clear();
        currentSequenceNames.Clear();
        stagesSolved.Add(false);
        startLocation.Add(lastStartPosition);
        int sequenceLength = Random.Range(3, 6);
        sequenceLengths.Add(sequenceLength);
        for (int i = 0; i < sequenceLength; i++)
        {
            int sequenceColour = Random.Range(0, 10);
            sequences.Add(sequenceColour);
            currentSequence.Add(sequenceColour);
            currentSequenceNames.Add(lightDevices[sequenceColour].colorName);
        }
        currentLevel++;
        counterText.text = currentLevel.ToString("00");
        indicator = Random.Range(0, 10);
        indicatorColour.Add(indicator);
        lastStartPosition = sequenceLength + startLocation[currentLevel - 1];
        Debug.LogFormat("[Simon's Stages #{0}] STAGE #{1}:", moduleId, currentLevel);
        Debug.LogFormat("[Simon's Stages #{0}] Sequence #{1}: {2}.", moduleId, currentLevel, string.Join(", ", currentSequenceNames.Select((x) => x).ToArray()));
        Debug.LogFormat("[Simon's Stages #{0}] Indicator #{1}: {2}.", moduleId, currentLevel, indicatorLights[indicator].colorName);
        CalculateSolution();
    }

    void CalculateSolution()
    {
        if (indicatorLights[indicator].colorName == "red")
        {
            for (int i = 0; i < currentSequence.Count; i++)
            {
                solutionNames.Add(lightDevices[currentSequence[i]].colorName);
                currentSolution.Add(currentSequence[i]);
                currentSolutionNames.Add(lightDevices[currentSequence[i]].colorName);
            }
        }
        else if (indicatorLights[indicator].colorName == "blue")
        {
            for (int i = currentSequence.Count - 1; i >= 0; i--)
            {
                solutionNames.Add(lightDevices[currentSequence[i]].colorName);
                currentSolution.Add(currentSequence[i]);
                currentSolutionNames.Add(lightDevices[currentSequence[i]].colorName);
            }
        }
        else if (indicatorLights[indicator].colorName == "yellow")
        {
            for (int i = 0; i < 2; i++)
            {
                solutionNames.Add(lightDevices[currentSequence[i]].colorName);
                currentSolution.Add(currentSequence[i]);
                currentSolutionNames.Add(lightDevices[currentSequence[i]].colorName);
            }
        }
        else if (indicatorLights[indicator].colorName == "orange")
        {
            for (int i = 1; i >= 0; i--)
            {
                solutionNames.Add(lightDevices[currentSequence[i]].colorName);
                currentSolution.Add(currentSequence[i]);
                currentSolutionNames.Add(lightDevices[currentSequence[i]].colorName);
            }
        }
        else if (indicatorLights[indicator].colorName == "magenta")
        {
            for (int i = currentSequence.Count - 2; i < currentSequence.Count; i++)
            {
                solutionNames.Add(lightDevices[currentSequence[i]].colorName);
                currentSolution.Add(currentSequence[i]);
                currentSolutionNames.Add(lightDevices[currentSequence[i]].colorName);
            }
        }
        else if (indicatorLights[indicator].colorName == "green")
        {
            for (int i = currentSequence.Count - 1; i >= (currentSequence.Count - 2); i--)
            {
                solutionNames.Add(lightDevices[currentSequence[i]].colorName);
                currentSolution.Add(currentSequence[i]);
                currentSolutionNames.Add(lightDevices[currentSequence[i]].colorName);
            }
        }
        else if (indicatorLights[indicator].colorName == "pink")
        {
            for (int i = 0; i < currentSequence.Count; i++)
            {
                solutionNames.Add(lightDevices[(5 + currentSequence[i]) % 10].colorName);
                currentSolution.Add(lightDevices[(5 + currentSequence[i]) % 10].colorIndex);
                currentSolutionNames.Add(lightDevices[(5 + currentSequence[i]) % 10].colorName);
            }
        }
        else if (indicatorLights[indicator].colorName == "lime")
        {
            for (int i = currentSequence.Count - 1; i >= 0; i--)
            {
                solutionNames.Add(lightDevices[(5 + currentSequence[i]) % 10].colorName);
                currentSolution.Add(lightDevices[(5 + currentSequence[i]) % 10].colorIndex);
                currentSolutionNames.Add(lightDevices[(5 + currentSequence[i]) % 10].colorName);
            }
        }
        else if (indicatorLights[indicator].colorName == "cyan")
        {
            int last = currentSequence.Count - 1;
            solutionNames.Add(lightDevices[(5 + currentSequence[0]) % 10].colorName);
            solutionNames.Add(lightDevices[(5 + currentSequence[last]) % 10].colorName);
            currentSolution.Add(lightDevices[(5 + currentSequence[0]) % 10].colorIndex);
            currentSolution.Add(lightDevices[(5 + currentSequence[last]) % 10].colorIndex);
            currentSolutionNames.Add(lightDevices[(5 + currentSequence[0]) % 10].colorName);
            currentSolutionNames.Add(lightDevices[(5 + currentSequence[last]) % 10].colorName);
        }
        else if (indicatorLights[indicator].colorName == "white")
        {
            solutionNames.Add(lightDevices[(5 + currentSequence[2]) % 10].colorName);
            solutionNames.Add(lightDevices[(5 + currentSequence[1]) % 10].colorName);
            currentSolution.Add(lightDevices[(5 + currentSequence[2]) % 10].colorIndex);
            currentSolution.Add(lightDevices[(5 + currentSequence[1]) % 10].colorIndex);
            currentSolutionNames.Add(lightDevices[(5 + currentSequence[2]) % 10].colorName);
            currentSolutionNames.Add(lightDevices[(5 + currentSequence[1]) % 10].colorName);
        }
        solutionStartLocation.Add(lastSolutionLocation);
        lastSolutionLocation = currentSolutionNames.Count + lastSolutionLocation;
        solveLengths.Add(currentSolution.Count);
        for (int i = 0; i < currentSolution.Count; i++)
        {
            lightsSolved.Add(false);
        }
        Debug.LogFormat("[Simon's Stages #{0}] Solution #{1}: {2}.", moduleId, currentLevel, string.Join(", ", currentSolutionNames.Select((x) => x).ToArray()));
        currentSolution.Clear();
        currentSolutionNames.Clear();
        DisplaySequence();
    }

    void DisplaySequence()
    {
        foreach (IndicatorInformation indic in indicatorLights)
        {
            indic.glow.enabled = false;
        }
        indicatorLights[indicatorColour[currentLevel - 1]].glow.enabled = true;
        indicatorText.text = lightTextOptions[indicatorLights[indicatorColour[currentLevel - 1]].colorIndex];
        indicatorLetter.Add(indicatorText.text);
        StartCoroutine(PlaySequence());
    }

    IEnumerator PlaySequence()
    {
        flashingCurStage = true;
        yield return new WaitForSeconds(0.5f);
        bool first = true;
        foreach (LightInformation device in lightDevices)
        {
            device.ledGlow.enabled = false;
            device.greyBase.enabled = true;
        }

        int current = currentLevel;
        while (current == currentLevel)
        {
            yield return new WaitForSeconds(0.1f);
            for (int i = 0; i < currentSequence.Count; i++)
            {
                if (current != currentLevel || readyToSolve)
                {
                    break;
                }
                if (first)
                {
                    Audio.PlaySoundAtTransform(lightDevices[currentSequence[i]].connectedSound.name, transform);
                }
                lightDevices[currentSequence[i]].ledGlow.enabled = true;
                lightDevices[currentSequence[i]].greyBase.enabled = false;
                yield return new WaitForSeconds(0.5f);
                if (current != currentLevel || readyToSolve)
                {
                    break;
                }
                lightDevices[currentSequence[i]].ledGlow.enabled = false;
                lightDevices[currentSequence[i]].greyBase.enabled = true;
                if (current != currentLevel || readyToSolve)
                {
                    break;
                }
                yield return new WaitForSeconds(0.25f);
            }
            first = false;
            foreach (LightInformation device in lightDevices)
            {
                device.ledGlow.enabled = false;
                device.greyBase.enabled = true;
            }
            yield return new WaitForSeconds(3f);
            flashingCurStage = false;
            yield return new WaitForSeconds(0);
        }
    }

    IEnumerator IndicatorBlink()
    {
        while (!moduleSolved)
        {
            foreach (IndicatorInformation indic in indicatorLights)
            {
                indic.glow.enabled = true;
            }
            yield return new WaitForSeconds(1.2f);
            if (moduleSolved || secondAttempt)
            {
                break;
            }
            foreach (IndicatorInformation indic in indicatorLights)
            {
                indic.glow.enabled = false;
            }
            yield return new WaitForSeconds(1.2f);
        }
        foreach (IndicatorInformation indic in indicatorLights)
        {
            indic.glow.enabled = false;
        }
    }

    void Start()
    {
        absoluteLevelPosition.Add(0);
        moduleCount = Bomb.GetSolvableModuleNames().Where(x => !ignoredModules.Contains(x)).Count();
        stagesToGenerate = moduleCount - 1;
        //moduleCount = 11;
        indicatorText.text = "";
        moduleLocked = true;
        foreach (LightInformation device in lightDevices)
        {
            device.colorIndex = Random.Range(0, 10);
            while (chosenIndices.Contains(device.colorIndex))
            {
                device.colorIndex = Random.Range(0, 10);
            }
            chosenIndices.Add(device.colorIndex);

            device.soundIndex = Random.Range(0, 10);
            while (chosenIndices2.Contains(device.soundIndex))
            {
                device.soundIndex = Random.Range(0, 10);
            }
            chosenIndices2.Add(device.soundIndex);

            float scalar = transform.lossyScale.x;
            device.ledGlow.range *= scalar;
            device.ledGlow.color = lightDeviceColorOptions[device.colorIndex];
            device.colorBase.material = lightBaseOptions[device.colorIndex];
            device.lightText.text = lightTextOptions[device.colorIndex];
            device.colorName = lightNameOptions[device.colorIndex];
            device.connectedSound = sfxOptions[device.soundIndex];
            device.ledGlow.enabled = false;
        }

        Debug.LogFormat("[Simon's Stages #{0}] The arrangement of colors is: {1} // {2}", moduleId,
            string.Join(", ", lightDevices.Take(5).Select(ld => ld.colorName).ToArray()),
            string.Join(", ", lightDevices.Skip(5).Select(ld => ld.colorName).ToArray()));

        chosenIndices.Clear();
        chosenIndices2.Clear();

        foreach (IndicatorInformation indic in indicatorLights)
        {
            indic.colorIndex = Random.Range(0, 10);
            while (chosenIndices.Contains(indic.colorIndex))
            {
                indic.colorIndex = Random.Range(0, 10);
            }
            chosenIndices.Add(indic.colorIndex);

            float scalar = transform.lossyScale.x;
            indic.glow.range *= scalar;
            indic.glow.color = lightDeviceColorOptions[indic.colorIndex];
            indic.colorName = lightNameOptions[indic.colorIndex];
            indic.glow.enabled = false;
        }
        chosenIndices.Clear();
        StartCoroutine(StartFlash());
    }

    IEnumerator StartFlash()
    {
        Audio.PlaySoundAtTransform("scaryRiff", transform);
        int index = 0;
        int iterations = 0;
        while (moduleLocked && iterations < 2)
        {
            currentLevel = Random.Range(0, 100);
            counterText.text = currentLevel.ToString("00");
            lightDevices[index].greyBase.enabled = false;
            lightDevices[index].ledGlow.enabled = true;
            indicatorLights[index].glow.enabled = true;
            indicatorText.text = lightTextOptions[indicatorLights[index].colorIndex];
            yield return new WaitForSeconds(0.05f);
            currentLevel = Random.Range(0, 100);
            counterText.text = currentLevel.ToString("00");
            lightDevices[index].greyBase.enabled = true;
            lightDevices[index].ledGlow.enabled = false;
            indicatorLights[index].glow.enabled = false;
            yield return new WaitForSeconds(0.025f);
            if (index < 10 && !reverse)
            {
                index++;
            }
            if (index == 10)
            {
                reverse = true;
                index = 9;
            }

            if (index >= 0 && reverse)
            {
                index--;
            }
            if (index < 0 && reverse)
            {
                if (iterations == 1)
                {
                    moduleLocked = false;
                }
                reverse = false;
                iterations++;
                index = 1;
            }
        }
        indicatorText.text = "";
        moduleLocked = true;
        int counter = 0;
        while (moduleLocked)
        {
            for (int i = 0; i <= 9; i++)
            {
                lightDevices[i].greyBase.enabled = false;
                lightDevices[i].ledGlow.enabled = true;
                indicatorLights[i].glow.enabled = true;
                currentLevel = Random.Range(0, 100);
                counterText.text = currentLevel.ToString("00");
            }
            yield return new WaitForSeconds(0.05f);
            for (int i = 0; i <= 9; i++)
            {
                lightDevices[i].greyBase.enabled = true;
                lightDevices[i].ledGlow.enabled = false;
                indicatorLights[i].glow.enabled = false;
                currentLevel = Random.Range(0, 100);
                counterText.text = currentLevel.ToString("00");
            }
            yield return new WaitForSeconds(0.05f);
            counter++;
            if (counter == 30)
            {
                moduleLocked = false;
                currentLevel = 0;
                counterText.text = currentLevel.ToString("00");
                gameOn = true;
            }
        }
    }

    public void ButtonPress(LightInformation device)
    {
        if (moduleSolved || moduleLocked || !gameOn || checking)
        {
            return;
        }
        else if (!readyToSolve)
        {
            device.connectedButton.AddInteractionPunch();
            GetComponent<KMBombModule>().HandleStrike();
            Debug.LogFormat("[Simon's Stages #{0}] Strike! The module is not yet ready to be solved.", moduleId);
            return;
        }
        // moduleLocked = true;
        if (secondAttemptLock)
        {
            secondAttemptLock = false;
        }

        Audio.PlaySoundAtTransform(device.connectedSound.name, transform);
        StartCoroutine(PressFlash(device));

        if (device.colorName == solutionNames[totalPresses])
        {
            lightsSolved[totalPresses] = true;
            clearLights.Add(totalPresses);
            Debug.LogFormat("[Simon's Stages #{0}] You pressed {1}. That is correct.", moduleId, device.colorName);
        }
        else
        {
            clearLights.Clear();
            Debug.LogFormat("[Simon's Stages #{0}] You pressed {1}. That is incorrect.", moduleId, device.colorName);
        }
        stagePresses++;
        totalPresses++;
        if (stagePresses == solveLengths[increaser])
        {
            device.connectedButton.AddInteractionPunch();
            stagePresses = 0;
            for (int i = solutionStartLocation[increaser]; i <= solutionStartLocation[increaser] + solveLengths[increaser] - 1; i++)
            {
                if (!lightsSolved[i])
                {
                    stagesSolved[increaser] = false;
                    break;
                }
                else
                {
                    stagesSolved[increaser] = true;
                }
            }
            if (stagesSolved[increaser])
            {
                result = "passed";
                for (int i = 0; i < clearLights.Count; i++)
                {
                    completeLights.Add(clearLights[i]);
                }
            }
            else
            {
                ReCompileLists();
            }
            clearLights.Clear();
            Debug.LogFormat("[Simon's Stages #{0}] END OF STAGE {1}. You {2} this stage.", moduleId, absoluteLevelPosition[increaser] + 1, result);
            increaser++;
            if (totalPresses < solutionNames.Count)
            {
                Debug.LogFormat("[Simon's Stages #{0}] STAGE {1} RESPONSE:", moduleId, absoluteLevelPosition[increaser] + 1, result);
            }
        }
        else
        {
            device.connectedButton.AddInteractionPunch(0.25f);
        }

        if (totalPresses >= solutionNames.Count)
        {
            moduleLocked = true;
            checking = true;
            CheckEndGame();
        }
    }

    void ReCompileLists()
    {
        result = "did not pass";
        incorrectSolveLengths.Add(solveLengths[increaser]);
        incorrectSequenceLength.Add(sequenceLengths[increaser]);
        if (incorrectSequenceStartLocation.Count == 0)
        {
            incorrectSequenceStartLocation.Add(0);
        }
        else
        {
            incorrectSequenceStartLocation.Add(incorrectSequenceStartLocation.Last() + sequenceLengths[tempCurrent.Last()]);
        }
        for (int i = 0; i < sequenceLengths[increaser]; i++)
        {
            repeatedSequence.Add(sequences[startLocation[increaser] + i]);
        }
        incorrectIndicatorLights.Add(indicatorColour[increaser]);
        incorrectIndicatorLetter.Add(indicatorLetter[increaser]);
        for (int i = 0; i < solveLengths[increaser]; i++)
        {
            tempCorrectSolution.Add(solutionNames[solutionStartLocation[increaser] + i]);
        }
        if (incorrectSolutionStartLocation.Count == 0)
        {
            incorrectSolutionStartLocation.Add(0);
        }
        else
        {
            incorrectSolutionStartLocation.Add(incorrectSolutionStartLocation.Last() + solveLengths[tempCurrent.Last()]);
        }
        tempCurrent.Add(increaser);
        tempAbsolute.Add(absoluteLevelPosition[increaser]);
        //stageIncreaser++;
    }
    void CheckEndGame()
    {
        for (int i = 0; i < stagesSolved.Count; i++)
        {
            if (!stagesSolved[i])
            {
                secondAttempt = true;
                break;
            }
            else
            {
                secondAttempt = false;
            }
        }

        int endGameCheck = stagesSolved.Count;
        if (endGameCheck == 1)
        {
            if (stagesSolved[0])
            {
                secondAttempt = false;
            }
        }

        if (!secondAttempt)
        {
            GetComponent<KMBombModule>().HandlePass();
            Debug.LogFormat("[Simon's Stages #{0}] Inputs correct. Module disarmed.", moduleId);
            moduleSolved = true;
            StartCoroutine(SolveLights());
        }
        else
        {
            secondAttemptLock = true;
            totalPresses = 0;
            stagePresses = 0;
            increaser = 0;
            stageIncreaser = 0;

            absoluteLevelPosition.Clear();
            for (int i = 0; i < tempAbsolute.Count; i++)
            {
                absoluteLevelPosition.Add(tempAbsolute[i]);
            }
            tempAbsolute.Clear();

            for (int i = completeLights.Count - 1; i >= 0; i--)
            {
                lightsSolved.RemoveAt(completeLights[i]);
            }
            completeLights.Clear();

            for (int i = lightsSolved.Count - 1; i >= 0; i--)
            {
                lightsSolved[i] = false;
            }
            for (int i = stagesSolved.Count - 1; i >= 0; i--)
            {
                if (stagesSolved[i])
                {
                    stagesSolved.RemoveAt(i);
                }
            }

            startLocation.Clear();
            for (int i = 0; i < incorrectSequenceStartLocation.Count; i++)
            {
                startLocation.Add(incorrectSequenceStartLocation[i]);
            }
            incorrectSequenceStartLocation.Clear();

            tempCurrent.Clear();

            sequenceLengths.Clear();
            for (int i = 0; i < incorrectSequenceLength.Count; i++)
            {
                sequenceLengths.Add(incorrectSequenceLength[i]);
            }
            incorrectSequenceLength.Clear();

            solveLengths.Clear();
            for (int i = 0; i < incorrectSolveLengths.Count; i++)
            {
                solveLengths.Add(incorrectSolveLengths[i]);
            }
            incorrectSolveLengths.Clear();

            solutionNames.Clear();
            for (int i = 0; i < tempCorrectSolution.Count; i++)
            {
                solutionNames.Add(tempCorrectSolution[i]);
            }
            tempCorrectSolution.Clear();

            solutionStartLocation.Clear();
            for (int i = 0; i < incorrectSolutionStartLocation.Count; i++)
            {
                solutionStartLocation.Add(incorrectSolutionStartLocation[i]);
            }
            incorrectSolutionStartLocation.Clear();

            indicatorLetter.Clear();
            for (int i = 0; i < incorrectIndicatorLetter.Count; i++)
            {
                indicatorLetter.Add(incorrectIndicatorLetter[i]);
            }
            incorrectIndicatorLetter.Clear();

            indicatorColour.Clear();
            for (int i = 0; i < incorrectIndicatorLights.Count; i++)
            {
                indicatorColour.Add(incorrectIndicatorLights[i]);
            }
            incorrectIndicatorLights.Clear();

            sequences.Clear();
            for (int i = 0; i < repeatedSequence.Count; i++)
            {
                sequences.Add(repeatedSequence[i]);
            }
            repeatedSequence.Clear();
            Debug.LogFormat("[Simon's Stages #{0}] Strike! Your sequence was incorrect. Please re-input stage(s) {1}.", moduleId, string.Join(", ", absoluteLevelPosition.Select((x) => (x + 1).ToString()).ToArray()));
            GetComponent<KMBombModule>().HandleStrike();
            Debug.LogFormat("[Simon's Stages #{0}] STAGE {1} RESPONSE:", moduleId, absoluteLevelPosition[0] + 1, result);
            StartCoroutine(RepeatSequence());
        }
    }

    IEnumerator RepeatSequence()
    {
        yield return new WaitForSeconds(2f);
        foreach (LightInformation device in lightDevices)
        {
            device.ledGlow.enabled = false;
            device.greyBase.enabled = true;
        }
        while (secondAttemptLock)
        {
            int j = 0;
            int k = 0;
            for (int i = 0; i < sequences.Count; i++)
            {
                counterText.text = (absoluteLevelPosition[j] + 1).ToString("00");
                indicatorText.text = indicatorLetter[j];
                indicatorLights[indicatorColour[j]].glow.enabled = true;
                Audio.PlaySoundAtTransform(lightDevices[sequences[i]].connectedSound.name, transform);
                if (!secondAttemptLock)
                {
                    break;
                }
                lightDevices[sequences[i]].ledGlow.enabled = true;
                lightDevices[sequences[i]].greyBase.enabled = false;
                yield return new WaitForSeconds(0.5f);
                if (!secondAttemptLock)
                {
                    break;
                }
                lightDevices[sequences[i]].ledGlow.enabled = false;
                lightDevices[sequences[i]].greyBase.enabled = true;
                if (!secondAttemptLock)
                {
                    break;
                }
                yield return new WaitForSeconds(0.25f);
                if (sequenceLengths[j] - 1 == k)
                {
                    j++;
                    k = 0;
                    foreach (IndicatorInformation indic in indicatorLights)
                    {
                        indic.glow.enabled = false;
                    }
                }
                else
                {
                    k++;
                }
            }
            foreach (LightInformation device in lightDevices)
            {
                device.ledGlow.enabled = false;
                device.greyBase.enabled = true;
            }
            counterText.text = "";
            indicatorText.text = "";
            j = 0;
            foreach (IndicatorInformation indic in indicatorLights)
            {
                indic.glow.enabled = false;
            }
            moduleLocked = false;
            checking = false;
            for (int x = 0; x < 20 && secondAttemptLock; x++) // Instead of having to wait 5 full seconds, interrupt early if secondAttemptLock is no longer enabled.
                yield return new WaitForSeconds(0.25f);
            if (secondAttemptLock)
            {
                moduleLocked = true;
            }
        }
    }

    IEnumerator PressFlash(LightInformation device)
    {
        device.greyBase.enabled = false;
        device.ledGlow.enabled = true;
        yield return new WaitForSeconds(0.5f);
        device.greyBase.enabled = true;
        device.ledGlow.enabled = false;
        moduleLocked = false;
    }

    IEnumerator SolveLights()
    {
        Audio.PlaySoundAtTransform("solveRiff", transform);
        int solveCounter = 0;
        while (solveCounter < 2)
        {
            yield return new WaitForSeconds(1f);
            for (int i = 0; i <= 9; i++)
            {
                lightDevices[i].greyBase.enabled = false;
                lightDevices[i].ledGlow.enabled = true;
                indicatorLights[i].glow.enabled = true;
                counterText.text = "";
            }
            yield return new WaitForSeconds(1f);
            for (int i = 0; i <= 9; i++)
            {
                lightDevices[i].greyBase.enabled = true;
                lightDevices[i].ledGlow.enabled = false;
                indicatorLights[i].glow.enabled = false;
                counterText.text = "";
            }
            solveCounter++;
        }
        yield return new WaitForSeconds(1f);
        for (int i = 0; i <= 9; i++)
        {
            lightDevices[i].greyBase.enabled = false;
            lightDevices[i].ledGlow.enabled = true;
            indicatorLights[i].glow.enabled = true;
            counterText.text = "";
        }
    }
}
