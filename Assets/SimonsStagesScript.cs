using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Rnd = UnityEngine.Random;

public class SimonsStagesScript : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMAudio Audio;
    public KMBossModule BossModule;
    public KMBombModule Module;

    public KMSelectable[] ButtonSels;
    public Light[] ButtonSpotlights;
    public Light[] IndicatorLights;
    public GameObject[] GrayBases;
    public MeshRenderer[] LightColorBases;
    public TextMesh[] LightTexts;
    public TextMesh IndicatorText;
    public Material[] ColorMats;
    public TextMesh StageText;

    private int _moduleId;
    private static int moduleIdCounter = 1;
    private bool _moduleSolved;
    private string[] _ignoredModules;
    private bool _notEnoughStages;
    private static readonly Color32[] Color32s = new Color32[10] { new Color32(255, 0, 0, 255), new Color32(10, 13, 255, 255), new Color32(251, 255, 0, 255), new Color32(255, 168, 8, 255), new Color32(100, 0, 101, 255), new Color32(47, 124, 0, 255), new Color32(255, 211, 255, 255), new Color32(122, 255, 0, 255), new Color32(11, 255, 255, 255), new Color32(255, 255, 255, 255) };
    private static readonly string[] _colorNames = new string[10] { "Red", "Blue", "Yellow", "Orange", "Magenta", "Green", "Pink", "Lime", "Cyan", "White" };

    private bool _canInteract;
    private int _stageCount;
    private int _currentSolves;
    private int _currentStage = -1;
    private bool _readyToAdvance;
    private bool _inputMode;
    private bool _isFlickering;
    private bool _dunGoofed;
    private readonly StageInfo _stageInfo = new StageInfo(0, null, null);
    private List<StageInfo> _stageInfos = new List<StageInfo>();
    private List<int> _remainingStagesToInput = new List<int>();
    private List<int> _correctStagesToInput = new List<int>();
    private int[] _buttonColors = new int[10];
    private int[] _indicatorColors = new int[10];
    private int[] _buttonSounds = new int[10];
    private Coroutine _flashStageAnim;
    private Coroutine[] _buttonPressAnims = new Coroutine[10];
    private Coroutine _indicatorBlinkAnim;
    private Coroutine _stageRecoveryAnim;

    public class StageInfo
    {
        public int Indicator;
        public int[] Flashes;
        public int[] Solution;

        public StageInfo(int sel, int[] f, int[] s)
        {
            Indicator = sel;
            Flashes = f;
            Solution = s;
        }

        public StageInfo GenerateStageInfo()
        {
            int indicColor = Rnd.Range(0, 10);
            int numFlashes = Rnd.Range(3, 6);
            var flashes = Enumerable.Range(0, numFlashes).Select(i => Rnd.Range(0, 10)).ToArray();
            int[] solution;
            var oppFlashes = flashes.Select(i => (i + 5) % 10).ToArray();
            switch (indicColor)
            {
                case 0: solution = flashes.ToArray(); break;
                case 1: solution = flashes.Reverse().ToArray(); break;
                case 2: solution = flashes.Take(2).ToArray(); break;
                case 3: solution = flashes.Take(2).Reverse().ToArray(); break;
                case 4: solution = flashes.Skip(flashes.Length - 2).ToArray(); break;
                case 5: solution = flashes.Skip(flashes.Length - 2).Reverse().ToArray(); break;
                case 6: solution = oppFlashes.ToArray(); break;
                case 7: solution = oppFlashes.Reverse().ToArray(); break;
                case 8: solution = new[] { oppFlashes.First(), oppFlashes.Last() }; break;
                case 9: solution = new[] { oppFlashes[2], oppFlashes[1] }; break;
                default: throw new InvalidOperationException("Invalid indicColor value");
            }
            return new StageInfo(indicColor, flashes, solution);
        }
    }

    private void Start()
    {
        _moduleId = moduleIdCounter++;
        for (int i = 0; i < ButtonSels.Length; i++)
            ButtonSels[i].OnInteract += ButtonPress(i);

        IndicatorText.text = "";
        _buttonColors = Enumerable.Range(0, 10).ToArray().Shuffle();
        _indicatorColors = Enumerable.Range(0, 10).ToArray().Shuffle();
        _buttonSounds = Enumerable.Range(0, 10).ToArray().Shuffle();
        for (int i = 0; i < 10; i++)
        {
            float scalar = transform.lossyScale.x;

            ButtonSpotlights[i].range *= scalar;
            ButtonSpotlights[i].color = Color32s[(int)_buttonColors[i]];
            ButtonSpotlights[i].enabled = false;

            IndicatorLights[i].range *= scalar;
            IndicatorLights[i].color = Color32s[(int)_indicatorColors[i]];
            IndicatorLights[i].enabled = false;

            LightTexts[i].text = _colorNames[_buttonColors[i]][0].ToString();
            LightColorBases[i].sharedMaterial = ColorMats[_buttonColors[i]];
        }

        Debug.LogFormat("[Simon's Stages #{0}] The arrangement of colors is: {1} // {2}", _moduleId,
            Enumerable.Range(0, 5).Select(i => _colorNames[_buttonColors[i]].ToString()).Join(", "),
            Enumerable.Range(5, 5).Select(i => _colorNames[_buttonColors[i]].ToString()).Join(", "));

        if (_ignoredModules == null)
            _ignoredModules = BossModule.GetIgnoredModules("Simon's Stages", new string[] { "Simon's Stages" });
        _stageCount = Bomb.GetSolvableModuleNames().Count(x => !_ignoredModules.Contains(x)) - 1;
        StartCoroutine(StartupAnimation());

        if (_stageCount <= 0)
        {
            Debug.LogFormat("[Simon's Stages #{0}] No stages to generate.", _moduleId);
            _notEnoughStages = true;
            return;
        }

        for (int i = 0; i < _stageCount; i++)
        {
            var stInfo = _stageInfo.GenerateStageInfo();
            _stageInfos.Add(stInfo);
            Debug.LogFormat("[Simon's Stages #{0}] STAGE #{1}:", _moduleId, i + 1);
            Debug.LogFormat("[Simon's Stages #{0}] Sequence #{1}: {2}.", _moduleId, i + 1, _stageInfos[i].Flashes.Select(x => _colorNames[_buttonColors[x]]).Join(", "));
            Debug.LogFormat("[Simon's Stages #{0}] Indicator #{1}: {2}.", _moduleId, i + 1, _colorNames[_stageInfos[i].Indicator]);
            Debug.LogFormat("[Simon's Stages #{0}] Solution #{1}: {2}.", _moduleId, i + 1, _stageInfos[i].Solution.Select(x => _colorNames[_buttonColors[x]]).Join(", "));
        }

    }

    private void Update()
    {
        if (!_readyToAdvance)
            return;
        _currentSolves = Bomb.GetSolvedModuleNames().Count(i => !_ignoredModules.Contains(i)) - 1;
        if (_currentStage == _currentSolves)
            return;
        if (_currentStage <= _stageCount)
            Advance();
    }

    private void Advance()
    {
        _currentStage++;
        if (_flashStageAnim != null)
            StopCoroutine(_flashStageAnim);
        for (int i = 0; i < 10; i++)
        {
            IndicatorLights[i].enabled = false;
            IndicatorText.text = "";
            StageText.text = "";
            DoFlash(i, false);
        }
        if (_currentStage != _stageCount)
        {
            StageText.text = ((_currentStage + 1) % 100).ToString("00");
            SetIndicator(_currentStage);
            _flashStageAnim = StartCoroutine(FlashStage());
        }
        else
        {
            Audio.PlaySoundAtTransform("scaryRiff", transform);
            _inputMode = true;
            _indicatorBlinkAnim = StartCoroutine(IndicatorBlink());
            _correctStagesToInput = Enumerable.Range(0, _stageCount).ToList();
            _remainingStagesToInput = _correctStagesToInput.ToList();
            _currentStageForInput = _correctStagesToInput.First();
            Debug.LogFormat("[Simon's Stages #{0}] STAGE {1} RESPONSE:", _moduleId, _currentStageForInput + 1);
        }
    }

    private void SetIndicator(int stage)
    {
        IndicatorLights[Array.IndexOf(_indicatorColors, _stageInfos[stage].Indicator)].enabled = true;
        IndicatorText.text = _colorNames[_stageInfos[stage].Indicator].ToString().Substring(0, 1);
    }

    private IEnumerator FlashStage()
    {
        _readyToAdvance = false;
        var st = _stageInfos[_currentStage];
        bool firstFlash = true;
        while (true)
        {
            yield return new WaitForSeconds(0.1f);
            for (int f = 0; f < st.Flashes.Length; f++)
            {
                var flash = st.Flashes[f];
                if (firstFlash)
                    Audio.PlaySoundAtTransform("press" + _buttonSounds[flash].ToString(), transform);
                DoFlash(flash, true);
                yield return new WaitForSeconds(0.5f);
                DoFlash(flash, false);
                yield return new WaitForSeconds(0.25f);
            }
            yield return new WaitForSeconds(3f);
            firstFlash = false;
            _readyToAdvance = true;
        }
    }

    private IEnumerator IndicatorBlink()
    {
        _isFlickering = true;
        while (true)
        {
            for (int i = 0; i < 10; i++)
                IndicatorLights[i].enabled = true;
            yield return new WaitForSeconds(1.2f);
            for (int i = 0; i < 10; i++)
                IndicatorLights[i].enabled = false;
            yield return new WaitForSeconds(1.2f);
        }
    }

    private void DoFlash(int ix, bool on)
    {
        GrayBases[ix].SetActive(!on);
        ButtonSpotlights[ix].enabled = on;
    }

    private IEnumerator StartupAnimation()
    {
        Audio.PlaySoundAtTransform("scaryRiff", transform);
        for (int iter = 0; iter < 2; iter++)
        {
            for (int i = -9; i < 10; i++)
            {
                StageText.text = Rnd.Range(0, 100).ToString("00");
                int ix = 9 - Math.Abs(i);
                GrayBases[ix].SetActive(false);
                ButtonSpotlights[ix].enabled = true;
                IndicatorLights[ix].enabled = true;
                IndicatorText.text = _colorNames[_indicatorColors[ix]][0].ToString();
                yield return new WaitForSeconds(0.05f);
                StageText.text = Rnd.Range(0, 100).ToString("00");
                GrayBases[ix].SetActive(true);
                ButtonSpotlights[ix].enabled = false;
                IndicatorLights[ix].enabled = false;
                yield return new WaitForSeconds(0.025f);
            }
        }
        IndicatorText.text = "";
        for (int counter = 0; counter < 30; counter++)
        {
            for (int ix = 0; ix < 10; ix++)
            {
                StageText.text = Rnd.Range(0, 100).ToString("00");
                GrayBases[ix].SetActive(false);
                ButtonSpotlights[ix].enabled = true;
                IndicatorLights[ix].enabled = true;
            }
            yield return new WaitForSeconds(0.05f);
            for (int ix = 0; ix < 10; ix++)
            {
                StageText.text = Rnd.Range(0, 100).ToString("00");
                GrayBases[ix].SetActive(true);
                ButtonSpotlights[ix].enabled = false;
                IndicatorLights[ix].enabled = false;
            }
            yield return new WaitForSeconds(0.025f);
        }
        StageText.text = "00";
        if (_notEnoughStages)
        {
            StartCoroutine(SolveLights());
            _moduleSolved = true;
            Module.HandlePass();
            yield break;
        }
        _readyToAdvance = true;
        _canInteract = true;
    }

    private List<int> _inputList = new List<int>();
    private int _currentStageForInput;
    private bool goodSoFar = true;

    private KMSelectable.OnInteractHandler ButtonPress(int btn)
    {
        return delegate ()
        {
            if (_moduleSolved || !_canInteract)
                return false;
            if (!_inputMode)
            {
                Debug.LogFormat("[Simon's Stages #{0}] Strike! The module is not yet ready to be solved.", _moduleId);
                ButtonSels[btn].AddInteractionPunch();
                Module.HandleStrike();
                return false;
            }
            Audio.PlaySoundAtTransform("press" + _buttonSounds[btn], transform);
            if (_stageRecoveryAnim != null)
                StopCoroutine(_stageRecoveryAnim);
            for (int i = 0; i < 10; i++)
            {
                if (!_isFlickering)
                    IndicatorLights[i].enabled = false;
                DoFlash(i, false);
            }
            StageText.text = "";

            if (_buttonPressAnims[btn] != null)
                StopCoroutine(_buttonPressAnims[btn]);
            _buttonPressAnims[btn] = StartCoroutine(ButtonPressAnimation(btn));

            _inputList.Add(btn);
            if (_inputList[_inputList.Count - 1] == _stageInfos[_currentStageForInput].Solution[_inputList.Count - 1])
                Debug.LogFormat("[Simon's Stages #{0}] You pressed {1}. That is correct.", _moduleId, _colorNames[_buttonColors[btn]]);
            else
            {
                Debug.LogFormat("[Simon's Stages #{0}] You pressed {1}. That is incorrect.", _moduleId, _colorNames[_buttonColors[btn]]);
                goodSoFar = false;
                _dunGoofed = true;
            }

            if (_inputList.Count != _stageInfos[_currentStageForInput].Solution.Length)
            {
                ButtonSels[btn].AddInteractionPunch(0.25f);
                return false;
            }
            ButtonSels[btn].AddInteractionPunch();
            if (goodSoFar)
            {
                Debug.LogFormat("[Simon's Stages #{0}] END OF STAGE {1}. You passed this stage.", _moduleId, _currentStageForInput + 1);
                _correctStagesToInput.Remove(_currentStageForInput);
            }
            else
                Debug.LogFormat("[Simon's Stages #{0}] END OF STAGE {1}. You did not pass this stage.", _moduleId, _currentStageForInput + 1);
            _inputList = new List<int>();
            _remainingStagesToInput.Remove(_currentStageForInput);
            goodSoFar = true;
            if (_remainingStagesToInput.Count != 0)
            {
                _currentStageForInput = _remainingStagesToInput.First();
                Debug.LogFormat("[Simon's Stages #{0}] STAGE {1} RESPONSE:", _moduleId, _currentStageForInput + 1);
            }
            else
                CheckAnswer();
            return false;
        };
    }

    private void CheckAnswer()
    {
        if (_indicatorBlinkAnim != null)
            StopCoroutine(_indicatorBlinkAnim);
        _isFlickering = false;
        if (_correctStagesToInput.Count == 0)
        {
            Debug.LogFormat("[Simon's Stages #{0}] Inputs correct. Module disarmed.", _moduleId);
            StartCoroutine(SolveLights());
            _moduleSolved = true;
            Module.HandlePass();
            return;
        }
        _canInteract = false;
        for (int i = 0; i < 10; i++)
            IndicatorLights[i].enabled = false;

        _remainingStagesToInput = _correctStagesToInput.ToList();
        _currentStageForInput = _correctStagesToInput.First();
        _stageRecoveryAnim = StartCoroutine(StageRecoveryAnimation());

        Debug.LogFormat("[Simon's Stages #{0}] Strike! Your sequence was incorrect. Please re-input stage(s) {1}.", _moduleId, _correctStagesToInput.Select(i => i + 1).Join(", "));
        Debug.LogFormat("[Simon's Stages #{0}] STAGE #{1} RESPONSE:", _moduleId, _currentStageForInput + 1);
        _dunGoofed = false;
        Module.HandleStrike();
    }

    private IEnumerator StageRecoveryAnimation()
    {
        yield return new WaitForSeconds(2f);
        while (true)
        {
            for (int i = 0; i < _correctStagesToInput.Count; i++)
            {
                int stage = _correctStagesToInput[i];
                StageText.text = ((stage + 1) % 100).ToString("00");
                var st = _stageInfos[stage];
                for (int f = 0; f < st.Flashes.Length; f++)
                {
                    var flash = st.Flashes[f];
                    Audio.PlaySoundAtTransform("press" + _buttonSounds[flash].ToString(), transform);
                    DoFlash(flash, true);
                    yield return new WaitForSeconds(0.5f);
                    DoFlash(flash, false);
                    yield return new WaitForSeconds(0.25f);
                }
            }
            StageText.text = "";
            _canInteract = true;
            yield return new WaitForSeconds(5f);
        }
    }

    private IEnumerator SolveLights()
    {
        Audio.PlaySoundAtTransform("solveRiff", transform);
        StageText.text = "";
        yield return new WaitForSeconds(1f);
        for (int x = 0; x < 2; x++)
        {
            for (int i = 0; i < 10; i++)
            {
                DoFlash(i, true);
                IndicatorLights[i].enabled = true;
            }
            yield return new WaitForSeconds(1f);
            for (int i = 0; i < 10; i++)
            {
                DoFlash(i, false);
                IndicatorLights[i].enabled = false;
            }
            yield return new WaitForSeconds(1f);
        }
        for (int i = 0; i < 10; i++)
        {
            DoFlash(i, true);
            IndicatorLights[i].enabled = true;
        }
    }

    private IEnumerator ButtonPressAnimation(int btn)
    {
        DoFlash(btn, true);
        yield return new WaitForSeconds(0.5f);
        DoFlash(btn, false);
    }

#pragma warning disable 0414
    private readonly string TwitchHelpMessage = "!{0} press <letters> [press a sequence of colors based on their first letter]";
#pragma warning restore 0414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.ToUpperInvariant();
        var m = Regex.Match(command, @"^\s*[RBYOMGPLCW,; ]+\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!m.Success)
            yield break;
        var list = new List<int>();
        for (int i = 0; i < command.Length; i++)
        {
            var str = "RBYOMGPLCW,; ";
            int ix = str.IndexOf(command[i]);
            if (ix > 9)
                continue;
            if (ix == -1)
                yield break;
            list.Add(ix);
        }
        if (!_canInteract)
        {
            yield return "sendtochaterror You cannot interact with the module right now. Command ignored.";
            yield break;
        }
        Debug.Log("a");
        yield return null;
        for (int i = 0; i < list.Count; i++)
        {
            int btnIx = Array.IndexOf(_buttonColors, list[i]);
            ButtonSels[btnIx].OnInteract();
            yield return new WaitForSeconds(0.1f);
        }
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        Debug.LogFormat("[Simon's Stages #{0}] Module force solved via autosolver.", _moduleId);
        while (!_inputMode || !_canInteract)
            yield return true;
        if (_dunGoofed)
        {
            if (_indicatorBlinkAnim != null)
                StopCoroutine(_indicatorBlinkAnim);
            if (_stageRecoveryAnim != null)
                StopCoroutine(_stageRecoveryAnim);
            StartCoroutine(SolveLights());
            _moduleSolved = true;
            Module.HandlePass();
            yield break;
        }
        while (!_moduleSolved)
        {
            ButtonSels[_stageInfos[_currentStageForInput].Solution[_inputList.Count]].OnInteract();
            yield return new WaitForSeconds(0.1f);
        }
    }
}
