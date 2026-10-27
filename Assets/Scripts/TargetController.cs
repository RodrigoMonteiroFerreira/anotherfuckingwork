using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using DG.Tweening;
using System.Collections;

public class TargetController : MonoBehaviour
{
    [Header("Yo-Yo")]
    public Transform yoYoOrigin;
    public LineRenderer stringLine;
    public float launchDuration = 0.9f;   // tempo que a bola demora a chegar
    public float ballDelay = 1f;          // espera (em segundos) até a bola começar a mexer
    public float tipDuration = 0.25f;     // tempo que o topo da linha demora a chegar ao rato
    public float ropeLength = 1.5f;       // distância a que a bola fica abaixo do rato
    public float curveStrength = 0.5f;

    [Header("Wobble (movimento aleatório)")]
    public float wobbleAmount = 1.2f;
    public int wobbleWaves = 2;

    [Header("Desaparecer a linha")]
    public bool hideLineAfterLanding = true;
    public float lineHideDelay = 0.3f;    // espera depois de a bola chegar
    public float lineFadeDuration = 0.3f; // tempo que a linha demora a desaparecer

    [Header("Game")]
    public float gameDuration = 30f;
    public Text scoreText;
    public Text timerText;

    [Header("Coins")]
    public GameObject coinPrefab;
    public float respawnDelay = 1f;       // segundos até aparecer outra moeda
    public int maxCoins = 10;             // máximo de moedas ao mesmo tempo no ecrã

    [Header("Áudio")]
    public AudioClip coinSound;                // som ao apanhar uma moeda
    [Range(0f, 1f)] public float coinVolume = 1f;
    private AudioSource audioSource;

    [Header("Tween Types (Ease)")]
    public Ease ballEase = Ease.OutCubic;      // movimento da bola (OutCubic = igual ao que tinhas)
    public Ease tipEase = Ease.OutQuad;        // DOMove do topo da linha
    public Ease coinSpawnEase = Ease.OutBack;  // DOScale da moeda a aparecer

    public bool useCustomPunchEase = false;    // se false, usa o ease por defeito do DOTween
    public Ease punchEase = Ease.OutQuad;      // DOPunchScale da bola

    public bool useCustomShakeEase = false;    // se false, usa o ease por defeito do DOTween
    public Ease shakeEase = Ease.Linear;       // DOShakePosition da câmara

    // Moedas já contadas (static: partilhado, caso o script esteja duplicado).
    private static readonly System.Collections.Generic.HashSet<GameObject> collectedCoins =
        new System.Collections.Generic.HashSet<GameObject>();

    private int score;
    private float timeLeft;
    private bool gameRunning;

    private Tween movementTween;
    private Tween tipTween;
    private Tween fadeTween;

    private Vector3 stringMid;
    private Transform anchor;    // topo da linha (objeto invisível criado por código)
    private Tween shakeTween;
    private Tween punchTween;
    private Vector3 baseScale;   // tamanho original da bola

    private const float LineWidth = 0.08f;

    void Start()
    {
        // DIAGNÓSTICO: avisa se houver mais do que uma cópia deste script na cena.
        int copies = FindObjectsByType<TargetController>(FindObjectsSortMode.None).Length;
        if (copies > 1)
            Debug.LogWarning("Há " + copies + " TargetController na cena! Deve haver só 1.", this);

        baseScale = transform.localScale;

        // AudioSource para os sons (usa o que já existe ou cria um).
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;

        if (stringLine != null)
        {
            stringLine.useWorldSpace = true;
            stringLine.positionCount = 20;
            stringLine.startWidth = LineWidth;
            stringLine.endWidth = LineWidth;
            stringLine.enabled = true;
        }

        // Objeto invisível que representa o topo da linha.
        // É ele que o DOMove vai mexer (o yoYoOrigin fica parado).
        anchor = new GameObject("YoYoAnchor").transform;
        anchor.position = yoYoOrigin != null ? yoYoOrigin.position : transform.position;
        anchor.position = new Vector3(anchor.position.x, anchor.position.y, 0f);

        // Começa com o ponto do meio já no sítio certo,
        // para a corda não "saltar" no primeiro frame.
        stringMid = (anchor.position + transform.position) * 0.5f;
        stringMid.z = 0f;

        UpdateScore();
        UpdateTimer();
        UpdateString();

        StartCoroutine(StartGame());
    }

    void Update()
    {
        UpdateString();

        if (!gameRunning)
            return;

        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            LaunchYoYo();
        }
    }

    void LaunchYoYo()
    {
        if (Camera.main == null || Mouse.current == null)
            return;

        Vector2 mousePosition = Mouse.current.position.ReadValue();

        Vector3 screenPosition = new Vector3(
            mousePosition.x,
            mousePosition.y,
            -Camera.main.transform.position.z
        );

        Vector3 destination = Camera.main.ScreenToWorldPoint(screenPosition);
        destination.z = transform.position.z;

        // Posição do rato no mundo (para onde vai o topo da linha).
        Vector3 mouseWorld = destination;

        // A bola vai ficar pendurada por baixo do rato.
        destination = mouseWorld + Vector3.down * ropeLength;

        Vector3 startPos = transform.position;
        Vector3 dir = destination - startPos;

        // Direção perpendicular ao movimento, usada para ondular de lado a lado.
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f).normalized;

        // Amplitude aleatória, para um lado ou para o outro.
        float amp = Random.Range(0.5f, 1f) * wobbleAmount
                    * (Random.value < 0.5f ? -1f : 1f);

        // Novo clique: volta a mostrar a linha e cancela o que estava a decorrer.
        ShowLine();

        // O topo da linha dispara para o rato.
        if (tipTween != null)
            tipTween.Kill();

        Vector3 anchorEnd = mouseWorld;
        anchorEnd.z = 0f;

        // DOMove: o topo da linha desliza até ao rato.
        tipTween = anchor
            .DOMove(anchorEnd, tipDuration)
            .SetEase(tipEase);

        // A bola vai atrás (depois de ballDelay segundos), com a ondulação.
        if (movementTween != null)
            movementTween.Kill();

        movementTween = DOVirtual.Float(0f, 1f, launchDuration, t =>
        {
            // Ease escolhido no Inspector (por defeito OutCubic, igual à fórmula antiga).
            float eased = DOVirtual.EasedValue(0f, 1f, t, ballEase);

            Vector3 pos = Vector3.Lerp(startPos, destination, eased);

            // A ondulação vai diminuindo até chegar ao destino.
            float wobble = Mathf.Sin(t * Mathf.PI * wobbleWaves)
                           * (1f - t) * amp;

            transform.position = pos + perp * wobble;
        })
        .SetDelay(ballDelay)
        .OnComplete(HideLine);
    }

    // Faz a linha ficar mais fina até desaparecer.
    void HideLine()
    {
        if (!hideLineAfterLanding || stringLine == null)
            return;

        fadeTween = DOVirtual.Float(LineWidth, 0f, lineFadeDuration, w =>
        {
            stringLine.startWidth = w;
            stringLine.endWidth = w;
        })
        .SetDelay(lineHideDelay)
        .OnComplete(() => stringLine.enabled = false);
    }

    // Volta a mostrar a linha (usado em cada novo clique).
    void ShowLine()
    {
        if (fadeTween != null)
            fadeTween.Kill();

        if (stringLine == null)
            return;

        stringLine.startWidth = LineWidth;
        stringLine.endWidth = LineWidth;
        stringLine.enabled = true;
    }

    void UpdateString()
    {
        if (stringLine == null || anchor == null)
            return;

        Vector3 start = anchor.position;
        Vector3 end = transform.position;

        start.z = 0f;
        end.z = 0f;

        float distance = (end - start).magnitude;

        // Ponto do meio com um pequeno atraso, para a corda balançar.
        Vector3 targetMid = (start + end) * 0.5f;
        targetMid.y -= Mathf.Min(curveStrength * distance, 2f);

        stringMid = Vector3.Lerp(stringMid, targetMid, Time.deltaTime * 8f);

        int points = 20;
        stringLine.positionCount = points;

        for (int i = 0; i < points; i++)
        {
            float t = i / (float)(points - 1);

            // Curva de Bézier quadrática.
            Vector3 point =
                (1f - t) * (1f - t) * start +
                2f * (1f - t) * t * stringMid +
                t * t * end;

            stringLine.SetPosition(i, point);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!gameRunning)
            return;

        // Procura o objeto mais "alto" com a tag Coin. Assim, se a moeda tiver
        // colliders em filhos, tratamos tudo como UMA só moeda.
        Transform coinT = null;
        for (Transform t = other.transform; t != null; t = t.parent)
        {
            if (t.CompareTag("Coin"))
                coinT = t;
        }

        if (coinT == null)
            return;

        GameObject coinObj = coinT.gameObject;

        // Se esta moeda já foi contada (por outro collider, ou por outra
        // cópia do script), ignora.
        if (!collectedCoins.Add(coinObj))
            return;

        // Desliga todos os colliders da moeda e tira-lhe a tag.
        foreach (Collider2D c in coinObj.GetComponentsInChildren<Collider2D>())
            c.enabled = false;

        coinObj.tag = "Untagged";
        other.tag = "Untagged";

        // DIAGNÓSTICO: deve aparecer UMA vez por moeda apanhada.
        Debug.Log("Moeda apanhada: " + coinObj.name + " | collider: " + other.name +
                  " | score antes: " + score, this);

        score++;
        UpdateScore();

        // Som de apanhar a moeda.
        if (coinSound != null)
            audioSource.PlayOneShot(coinSound, coinVolume);

        // Punch no scale: não interfere com o movimento da bola.
        // Cancela o soco anterior e volta ao tamanho original antes de
        // fazer outro, para a bola nunca ir ficando maior.
        if (punchTween != null)
            punchTween.Kill();

        transform.localScale = baseScale;

        punchTween = transform.DOPunchScale(baseScale * 0.15f, 0.25f);

        if (useCustomPunchEase)
            punchTween.SetEase(punchEase);

        // DOShakePosition: abana a câmara. (Se abanássemos a bola,
        // lutava com o movimento dela e mudava o comportamento.)
        Camera cam = Camera.main;

        if (cam != null)
        {
            if (shakeTween != null && shakeTween.IsActive())
                shakeTween.Complete();

            shakeTween = cam.transform.DOShakePosition(
                0.25f, new Vector3(0.2f, 0.2f, 0f), 20, 90f, false, true);

            if (useCustomShakeEase)
                shakeTween.SetEase(shakeEase);
        }

        Vector3 coinPosition = coinObj.transform.position;

        Destroy(coinObj);

        // Passado respawnDelay segundos, aparece outra moeda noutro sítio.
        StartCoroutine(RespawnCoin(coinPosition));
    }

    IEnumerator StartGame()
    {
        yield return new WaitForSeconds(1f);

        timeLeft = gameDuration;
        gameRunning = true;

        // Só aparece 1 moeda no início. Cada vez que apanhares uma,
        // aparece outra (ver RespawnCoin).
        SpawnCoin();

        while (timeLeft > 0f)
        {
            timeLeft -= Time.deltaTime;
            UpdateTimer();
            yield return null;
        }

        timeLeft = 0f;
        gameRunning = false;
        UpdateTimer();

        if (movementTween != null)
            movementTween.Kill();

        if (tipTween != null)
            tipTween.Kill();

        Debug.Log("Fim do jogo! Pontuação final: " + score);
    }

    // Espera um bocadinho e faz aparecer outra moeda.
    IEnumerator RespawnCoin(Vector3 oldPosition)
    {
        yield return new WaitForSeconds(respawnDelay);

        // Só aparece outra se o jogo ainda estiver a correr.
        // (O limite de moedas no ecrã é verificado dentro do SpawnCoin.)
        if (gameRunning)
            SpawnCoin(oldPosition);
    }

    // Se for dada uma posição para evitar, a nova moeda aparece longe dela.
    void SpawnCoin(Vector3? avoid = null)
    {
        if (coinPrefab == null)
            return;

        // Limite: conta as moedas que realmente existem no ecrã.
        // Se já houver o máximo, não cria mais nenhuma.
        if (GameObject.FindGameObjectsWithTag("Coin").Length >= maxCoins)
            return;

        Vector3 position = Vector3.zero;

        for (int tries = 0; tries < 10; tries++)
        {
            position = new Vector3(
                Random.Range(-6f, 6f),
                Random.Range(-3f, 3f),
                0f
            );

            if (avoid == null || Vector3.Distance(position, avoid.Value) >= 3f)
                break;
        }

        GameObject coin = Instantiate(coinPrefab, position, Quaternion.identity);

        // DIAGNÓSTICO: deve aparecer UMA vez por moeda criada.
        Debug.Log("Moeda criada em " + position, coin);

        coin.tag = "Coin";
        coin.transform.localScale = Vector3.zero;

        coin.transform.DOScale(1f, 0.3f).SetEase(coinSpawnEase);
    }

    void UpdateScore()
    {
        if (scoreText != null)
            scoreText.text = "Moedas: " + score;
    }

    void UpdateTimer()
    {
        if (timerText != null)
            timerText.text = "Tempo: " + Mathf.CeilToInt(timeLeft);
    }
}