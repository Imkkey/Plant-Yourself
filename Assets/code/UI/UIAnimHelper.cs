using System.Collections;
using UnityEngine;
using TMPro;

public static class UIAnimHelper
{
    public static IEnumerator AnimateLobbyAppearance(
        GameObject panelLobby, 
        RectTransform signWindow, 
        RectTransform playersPanel, 
        CanvasGroup codeGroup, 
        TMP_Text[] hostCodeDigits, 
        float duration, 
        Vector2 signPos, 
        Vector2 playersPos,
        bool showHostSign)
    {
        if (panelLobby == null) yield break;

        // 1. Подготовка: разносим элементы за экран и прячем цифры
        if (signWindow != null)
            signWindow.anchoredPosition = signPos + new Vector2(0, Screen.height + 500f);
        
        if (hostCodeDigits != null)
        {
            foreach (var digit in hostCodeDigits)
            {
                if (digit != null)
                {
                    Color c = digit.color;
                    c.a = 0f;
                    digit.color = c;
                    digit.transform.localScale = Vector3.zero;
                }
            }
        }

        if (codeGroup != null)
        {
            codeGroup.alpha = 1f;
            codeGroup.transform.localScale = Vector3.one;
        }

        if (playersPanel != null)
            playersPanel.anchoredPosition = playersPos - new Vector2(0, Screen.height + 500f);

        // Включаем саму панель лобби и её детей, на случай если они были выключены вручную
        panelLobby.SetActive(true);
        if (playersPanel != null) playersPanel.gameObject.SetActive(true);

        if (showHostSign)
        {
            if (signWindow != null) signWindow.gameObject.SetActive(true);
            if (codeGroup != null) codeGroup.gameObject.SetActive(true);
        }
        else
        {
            if (signWindow != null) signWindow.gameObject.SetActive(false);
            if (codeGroup != null) codeGroup.gameObject.SetActive(false);
        }

        float elapsed = 0f;

        // 2. Анимация: табличка падает сверху
        if (showHostSign && signWindow != null)
        {
            Vector2 startPosOffset = signWindow.anchoredPosition;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float p = 1f - Mathf.Pow(1f - t, 3f); // Cubic ease out
                signWindow.anchoredPosition = Vector2.Lerp(startPosOffset, signPos, p);
                yield return null;
            }
            signWindow.anchoredPosition = signPos;
        }

        // 3. Панель игроков выезжает снизу
        if (playersPanel != null)
        {
            elapsed = 0f;
            Vector2 startPosOffset = playersPanel.anchoredPosition;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float p = 1f - Mathf.Pow(1f - t, 3f); // Cubic ease out
                playersPanel.anchoredPosition = Vector2.Lerp(startPosOffset, playersPos, p);
                yield return null;
            }
            playersPanel.anchoredPosition = playersPos;
        }
    }

    public static IEnumerator AnimateLobbyDisappearance(
        GameObject panelLobby,
        RectTransform signWindow,
        RectTransform playersPanel,
        CanvasGroup codeGroup,
        float duration,
        Vector2 signPos,
        Vector2 playersPos,
        System.Action onComplete)
    {
        if (panelLobby == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        float elapsed = 0f;

        // 1. Быстро прячем цифры кода
        if (codeGroup != null)
        {
            Vector3 startScale = codeGroup.transform.localScale;
            while (elapsed < 0.2f)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / 0.2f;
                codeGroup.alpha = Mathf.Lerp(1f, 0f, t);
                codeGroup.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                yield return null;
            }
            codeGroup.alpha = 0f;
            codeGroup.transform.localScale = Vector3.zero;
        }

        // 2. Сначала прячем список игроков (уезжает вниз)
        elapsed = 0f;
        Vector2 startPlayersPos = playersPanel != null ? playersPanel.anchoredPosition : Vector2.zero;
        Vector2 targetPlayersPos = playersPos - new Vector2(0, Screen.height + 500f);

        if (playersPanel != null)
        {
            while (elapsed < duration * 0.8f) // Чуть быстрее
            {
                elapsed += Time.deltaTime;
                float t = elapsed / (duration * 0.8f);
                float p = t * t; // Ease-In
                playersPanel.anchoredPosition = Vector2.Lerp(startPlayersPos, targetPlayersPos, p);
                yield return null;
            }
            playersPanel.anchoredPosition = targetPlayersPos;
        }

        // 3. После этого прячем табличку лобби (улетает вверх)
        elapsed = 0f;
        Vector2 startSignPos = signWindow != null ? signWindow.anchoredPosition : Vector2.zero;
        Vector2 targetSignPos = signPos + new Vector2(0, Screen.height + 500f);

        if (signWindow != null)
        {
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float p = t * t; // Ease-In
                signWindow.anchoredPosition = Vector2.Lerp(startSignPos, targetSignPos, p);
                yield return null;
            }
            signWindow.anchoredPosition = targetSignPos;
        }

        panelLobby.SetActive(false);
        onComplete?.Invoke();
    }

    public static IEnumerator AnimateJoinAppearance(
        GameObject panelJoinCode,
        RectTransform joinPanelWindow,
        TMP_InputField[] joinCodeInputs,
        float duration,
        Vector2 originalPos,
        MonoBehaviour runner)
    {
        if (panelJoinCode == null) yield break;
        
        // Включаем саму панель и её внутреннее окно
        panelJoinCode.SetActive(true);
        RectTransform targetWindow = joinPanelWindow != null ? joinPanelWindow : panelJoinCode.GetComponent<RectTransform>();
        if (targetWindow != null) targetWindow.gameObject.SetActive(true);

        // Скрываем инпуты
        if (joinCodeInputs != null)
        {
            foreach (var inp in joinCodeInputs)
            {
                if (inp != null)
                {
                    inp.transform.localScale = Vector3.zero;
                    inp.interactable = false;
                }
            }
        }

        // 1. Падение панели
        if (targetWindow != null)
        {
            targetWindow.anchoredPosition = originalPos + new Vector2(0, Screen.height + 500f);
            
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float p = 1f - Mathf.Pow(1f - t, 3f); // Ease-Out
                targetWindow.anchoredPosition = Vector2.Lerp(originalPos + new Vector2(0, Screen.height + 500f), originalPos, p);
                yield return null;
            }
            targetWindow.anchoredPosition = originalPos;
        }

        // 2. Анимация цифр (инпутов)
        if (joinCodeInputs != null)
        {
            float digitAnimDuration = 0.35f;
            float delayBetweenDigits = 0.05f;

            for (int i = 0; i < joinCodeInputs.Length; i++)
            {
                if (joinCodeInputs[i] == null) continue;
                RectTransform rt = joinCodeInputs[i].GetComponent<RectTransform>();
                if (rt != null) runner.StartCoroutine(AnimateSingleDigitScale(rt, digitAnimDuration));
                yield return new WaitForSeconds(delayBetweenDigits);
            }
            
            foreach (var inp in joinCodeInputs)
            {
                if (inp != null) inp.interactable = true;
            }
            if (joinCodeInputs.Length > 0 && joinCodeInputs[0] != null) joinCodeInputs[0].Select();
        }
    }

    public static IEnumerator AnimateJoinDisappearance(
        GameObject panelJoinCode,
        RectTransform joinPanelWindow,
        float duration,
        Vector2 originalPos,
        System.Action onComplete)
    {
        if (panelJoinCode == null)
        {
            onComplete?.Invoke();
            yield break;
        }

        RectTransform targetWindow = joinPanelWindow != null ? joinPanelWindow : panelJoinCode.GetComponent<RectTransform>();

        if (targetWindow != null)
        {
            float elapsed = 0f;
            Vector2 startPos = targetWindow.anchoredPosition;
            Vector2 targetPos = originalPos + new Vector2(0, Screen.height + 500f);
            
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float p = t * t; // Ease-In
                targetWindow.anchoredPosition = Vector2.Lerp(startPos, targetPos, p);
                yield return null;
            }
        }
        
        panelJoinCode.SetActive(false);
        onComplete?.Invoke();
    }

    public static IEnumerator AnimateDigitsWithDelay(TMP_Text[] hostCodeDigits, float delaySeconds, MonoBehaviour runner)
    {
        yield return new WaitForSeconds(delaySeconds);

        if (hostCodeDigits != null && hostCodeDigits.Length > 0)
        {
            float digitAnimDuration = 0.4f;
            float delayBetweenDigits = 0.08f;

            for (int i = 0; i < hostCodeDigits.Length; i++)
            {
                if (hostCodeDigits[i] == null) continue;
                runner.StartCoroutine(AnimateSingleDigit(hostCodeDigits[i], digitAnimDuration));
                yield return new WaitForSeconds(delayBetweenDigits);
            }
        }
    }

    public static IEnumerator AnimateSingleDigit(TMP_Text digitText, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            Color c = digitText.color;
            c.a = Mathf.Lerp(0f, 1f, t * 2f);
            digitText.color = c;

            float p = t - 1f;
            float scale = 1f + 2.70158f * p * p * p + 1.70158f * p * p;
            scale = Mathf.Max(0f, scale);
            
            digitText.transform.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }

        Color finalC = digitText.color;
        finalC.a = 1f;
        digitText.color = finalC;
        digitText.transform.localScale = Vector3.one;
    }

    public static IEnumerator AnimateSingleDigitScale(RectTransform t, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float tVal = elapsed / duration;
            float p = tVal - 1f;
            float scale = 1f + 2.70158f * p * p * p + 1.70158f * p * p;
            scale = Mathf.Max(0f, scale);
            
            t.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }
        t.localScale = Vector3.one;
    }
}
