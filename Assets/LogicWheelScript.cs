using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Rnd = UnityEngine.Random;
using KModkit;

public class LogicWheelScript : MonoBehaviour
{
    public KMBombModule Module;
    public KMBombInfo BombInfo;
    public KMAudio Audio;

    public KMSelectable RegenSel;
    public KMSelectable SubmitSel;
    public KMSelectable[] ArrowSels;
    public TextMesh[] ScreenTexts;

    public enum LogicGate
    {
        OR,
        AND,
        NAND,
        NOR,
        XOR
    }

    private int _moduleId;
    private static int _moduleIdCounter = 1;
    private bool _moduleSolved;

    private LogicGate? _curLogicGate;
    private int[] _curDigits = new int[5];
    private int[] _curBinary = new int[5];
    private int[] _shuffleOrder = new int[5];
    private int[] _logicNumbers = new int[2];
    private int _logicResult;
    private int[] _unusedNumbers = new int[2];
    private int _solution;
    private int _input;
    private bool _hasGenerated;
    private bool _inSubmissionPhase;
    private bool _isAnimating;

    private void Start()
    {
        _moduleId = _moduleIdCounter++;
        for (int i = 0; i < ArrowSels.Length; i++)
            ArrowSels[i].OnInteract += ArrowPress(i);
        RegenSel.OnInteract += RegenPress;
        SubmitSel.OnInteract += SubmitPress;
    }

    private bool RegenPress()
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, RegenSel.transform);
        RegenSel.AddInteractionPunch(0.5f);
        if (_moduleSolved || _isAnimating)
            return false;
        if (_inSubmissionPhase || !_hasGenerated)
        {
            _shuffleOrder = Enumerable.Range(0, 5).ToArray().Shuffle();
            _inSubmissionPhase = false;
            _hasGenerated = true;
            _curLogicGate = null;
        }
        _curDigits = GenerateDigits().ToArray();
        _logicNumbers[0] =  _curDigits[0];
        _logicNumbers[1] =  _curDigits[1];
        _unusedNumbers[0] = _curDigits[2];
        _unusedNumbers[1] = _curDigits[3];
        _logicResult =      _curDigits[4];
        var shuffleDisplay = new int[5];
        for (int i = 0; i < 5; i++)
            shuffleDisplay[i] = _curDigits[_shuffleOrder[i]];
        Debug.LogFormat("[Logic Wheel #{0}] Displayed digits are: {1}", _moduleId, shuffleDisplay.Join(", "));
        Debug.LogFormat("[Logic Wheel #{0}] {1} {2} {3} = {4}. Unused numbers are {5} and {6}.", _moduleId, _logicNumbers[0], _curLogicGate, _logicNumbers[1], _logicResult, _unusedNumbers[0], _unusedNumbers[1]);
        for (int i = 0; i < 5; i++)
            ScreenTexts[i].text = shuffleDisplay[i].ToString();
        return false;
    }

    private bool SubmitPress()
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, SubmitSel.transform);
        SubmitSel.AddInteractionPunch(0.5f);
        if (_moduleSolved || _isAnimating)
            return false;
        if (!_hasGenerated)
        {
            Module.HandleStrike();
            Debug.LogFormat("[Logic Wheel #{0}] Attempted to go to submission phase without generating numbers. Strike.", _moduleId);
            return false;
        }
        if (!_inSubmissionPhase)
        {
            _solution = GetLogicResult(_unusedNumbers[0], _unusedNumbers[1], _curLogicGate.Value);
            _inSubmissionPhase = true;
            for (int i = 0; i < ScreenTexts.Length; i++)
            {
                _curBinary[i] = 0;
                ScreenTexts[i].text = _curBinary[i].ToString();
            }
            Debug.LogFormat("[Logic Wheel #{0}] Moved to submission phase. Unused numbers wre {1} and {2}. Solution: {3}", _moduleId, _unusedNumbers[0], _unusedNumbers[1], _solution);
            return false;
        }
        if (_inSubmissionPhase)
            StartCoroutine(CheckInput());
        return false;
    }

    private KMSelectable.OnInteractHandler ArrowPress(int i)
    {
        return delegate ()
        {
            Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, ArrowSels[i].transform);
            ArrowSels[i].AddInteractionPunch(0.2f);
            if (_moduleSolved || !_inSubmissionPhase || _isAnimating)
                return false;
            _curBinary[i] = (_curBinary[i] + 1) % 2;
            ScreenTexts[i].text = _curBinary[i].ToString();
            return false;
        };
    }

    private IEnumerable<int> GenerateDigits()
    {
        if (_curLogicGate == null)
        {
            _curLogicGate = (LogicGate)Rnd.Range(0, 5);
            Debug.LogFormat("[Logic Wheel #{0}] Regenerated new logic gate: {1}", _moduleId, _curLogicGate);
        }
        tryAgain:
        var nums = Enumerable.Range(0, 31).ToArray().Shuffle().Take(4).ToArray();
        var list = new List<int>();
        list.AddRange(nums);
        list.Add(GetLogicResult(nums[0], nums[1], _curLogicGate.Value));
        if (list.Distinct().Count() != 5 || list.Contains(0) || list.Contains(31))
            goto tryAgain;
        return list;
    }

    private int GetLogicResult(int a, int b, LogicGate l)
    {
        if (l == LogicGate.OR)
            return a | b;
        else if (l == LogicGate.AND)
            return a & b;
        else if (l == LogicGate.NAND)
            return (a & b) ^ 31;
        else if (l == LogicGate.NOR)
            return (a | b) ^ 31;
        else
            return a ^ b;
    }

    private IEnumerator CheckInput()
    {
        _isAnimating = true;
        _input = _curBinary[0] * 16 + _curBinary[1] * 8 + _curBinary[2] * 4 + _curBinary[3] * 2 + _curBinary[4];
        Audio.PlaySoundAtTransform("CheckSFX", transform);
        for (int i = 0; i < 5; i++)
        {
            ScreenTexts[i].color = new Color32(0, 255, 255, 255);
            yield return new WaitForSeconds(0.473f);
        }
        if (_input == _solution)
        {
            _moduleSolved = true;
            Module.HandlePass();
            Audio.PlaySoundAtTransform("SolveSFX", transform);
            for (int i = 0; i < 5; i++)
                ScreenTexts[i].color = new Color32(0, 255, 0, 255);
            Debug.LogFormat("[Logic Wheel #{0}] Successfully submitted {1}. Module solved.", _moduleId, _solution);
            yield break;
        }
        Module.HandleStrike();
        for (int i = 0; i < 5; i++)
            ScreenTexts[i].color = new Color32(255, 0, 0, 255);
        Debug.LogFormat("[Logic Wheel #{0}] Incorrectly submitted {1}. Strike.", _moduleId, _input);
        yield return new WaitForSeconds(1f);
        for (int i = 0; i < 5; i++)
        {
            ScreenTexts[i].text = "--";
            ScreenTexts[i].color = new Color32(255, 255, 255, 255);
        }
        _isAnimating = false;
        _hasGenerated = false;
        _inSubmissionPhase = false;
        yield break;
    }

#pragma warning disable 0414
    private readonly string TwitchHelpMessage = "!{0} regen [Regenerate new numbers] | !{0} submit [Press the submit button.] | !{0} submit 10101 [Submit 10101 as your answer]";
#pragma warning restore 0414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.Trim().ToLowerInvariant();
        var m = Regex.Match(command, @"^\s*regen\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            yield return null;
            RegenSel.OnInteract();
            yield break;
        }
        m = Regex.Match(command, @"^\s*submit\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (m.Success)
        {
            yield return null;
            SubmitSel.OnInteract();
            yield break;
        }
        if (command.StartsWith("submit "))
        {
            var submission = command.Substring(6);
            var list = new List<int>();
            string str = "01 ";
            for (int i = 0; i < submission.Length; i++)
            {
                int ix = str.IndexOf(submission[i]);
                if (ix == 2)
                    continue;
                if (ix == -1)
                    yield break;
                list.Add(ix);
            }
            yield return null;
            yield return "solve";
            yield return "strike";
            for (int i = 0; i < 5; i++)
            {
                if (list[i] != _curDigits[i] && _hasGenerated)
                {
                    ArrowSels[i].OnInteract();
                    yield return new WaitForSeconds(0.1f);
                }
            }
            SubmitSel.OnInteract();
        }
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        while (_isAnimating)
            yield return true;
        if (!_hasGenerated)
        {
            RegenSel.OnInteract();
            yield return new WaitForSeconds(0.1f);
        }
        if (!_inSubmissionPhase)
        {
            SubmitSel.OnInteract();
            yield return new WaitForSeconds(0.1f);
        }
        yield return new WaitForSeconds(0.1f);
        int[] targetBinary = new int[5] { (_solution / 16) % 2, (_solution / 8) % 2, (_solution / 4) % 2, (_solution / 2) % 2, _solution % 2 };
        for (int i = 0; i < 5; i++)
        {
            if (targetBinary[i] != _curBinary[i] && _hasGenerated)
            {
                ArrowSels[i].OnInteract();
                yield return new WaitForSeconds(0.1f);
            }
        }
        SubmitSel.OnInteract();
        while (!_moduleSolved)
            yield return true;
    }
}