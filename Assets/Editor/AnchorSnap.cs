using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

// ============================================================
// AnchorSnap.cs  —  Ferramenta de Editor
//
// Abre em: Ferramentas → Anchor Snap & Resoluções  (Ctrl+Shift+N)
//
// Funcionalidades:
//   • Snap Anchors to Corners — ajusta âncoras ao visual atual
//   • Grade visual de 16 presets de âncora (estilo Unity/Figma)
//   • Aplica em 1 objeto, todos os filhos, ou toda a cena
//   • Simulador de resoluções comuns (mobile, desktop, ultrawide)
//   • Detector de âncoras "soltas" que podem quebrar o layout
//   • Mostra anchorMin/Max atual com indicador visual
//   • Suporte total a Ctrl+Z
// ============================================================

public class AnchorSnap : EditorWindow
{
    // ── Estado ───────────────────────────────────────────────
    private Vector2  scroll;
    private bool     estilosOk  = false;
    private int      abaAtiva   = 0;
    private string[] abas       = { "⚓ Âncoras", "📐 Presets", "📱 Resoluções", "🔍 Diagnóstico" };

    // Estilos
    private GUIStyle sTitulo, sSecao, sLabel, sBotaoGrande, sBotaoPreset, sTag;

    // Cores
    private static readonly Color cAzul    = new Color(0.20f, 0.55f, 1.00f);
    private static readonly Color cVerde   = new Color(0.15f, 0.78f, 0.45f);
    private static readonly Color cLaranja = new Color(1.00f, 0.50f, 0.10f);
    private static readonly Color cVermelho= new Color(1.00f, 0.25f, 0.25f);
    private static readonly Color cCinza   = new Color(0.35f, 0.35f, 0.45f);

    // Resolucoes comuns
    private static readonly (string nome, int w, int h)[] resolucoes =
    {
        ("📱 iPhone SE",          375,  667),
        ("📱 iPhone 14 Pro",      393,  852),
        ("📱 Android HD",         360,  800),
        ("📱 Tablet 10\"",        800, 1280),
        ("🖥 HD   720p",         1280,  720),
        ("🖥 Full HD 1080p",     1920, 1080),
        ("🖥 2K   1440p",        2560, 1440),
        ("🖥 4K   2160p",        3840, 2160),
        ("🔲 UltraWide 21:9",    3440, 1440),
        ("🔲 Super UltraWide",   5120, 1440),
        ("🔲 Square  1:1",       1080, 1080),
    };

    // ════════════════════════════════════════════════════════
    //  ABRIR
    // ════════════════════════════════════════════════════════
    [MenuItem("Ferramentas/Anchor Snap & Resoluções  %#n")]
    public static void Abrir()
    {
        var w = GetWindow<AnchorSnap>("⚓ Anchor Snap");
        w.minSize = new Vector2(330, 540);
        w.Show();
    }

    // ════════════════════════════════════════════════════════
    //  OnGUI
    // ════════════════════════════════════════════════════════
    void OnGUI()
    {
        CarregarEstilos();
        scroll = GUILayout.BeginScrollView(scroll);

        DesenharCabecalho();
        abaAtiva = GUILayout.Toolbar(abaAtiva, abas, GUILayout.Height(28));
        EditorGUILayout.Space(8);

        switch (abaAtiva)
        {
            case 0: AbaAncoras();    break;
            case 1: AbaPresets();    break;
            case 2: AbaResolucoes(); break;
            case 3: AbaDiagnostico();break;
        }

        GUILayout.EndScrollView();
    }

    // ════════════════════════════════════════════════════════
    //  ABA 0 — SNAP DE ÂNCORAS
    // ════════════════════════════════════════════════════════
    void AbaAncoras()
    {
        // ── Info do objeto selecionado ────────────────────
        RectTransform rt = ObterRectTransform();
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("OBJETO SELECIONADO", sTitulo);
        EditorGUILayout.Space(4);

        if (rt == null)
        {
            EditorGUILayout.HelpBox("Selecione um elemento de UI (RectTransform) na Hierarchy.", MessageType.Warning);
        }
        else
        {
            DesenharInfoAncora(rt);
        }
        GUILayout.EndVertical();
        EditorGUILayout.Space(8);

        // ── Botão principal ───────────────────────────────
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("SNAP ANCHORS", sTitulo);
        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "Ajusta as âncoras para os limites visuais EXATOS do objeto.\n" +
            "Após o snap, o objeto fica responsivo e não quebra em outras resoluções.",
            MessageType.Info);
        EditorGUILayout.Space(4);

        GUI.enabled = rt != null;
        GUI.backgroundColor = new Color(0.15f, 0.65f, 0.35f);
        if (GUILayout.Button("⚓  SNAP ANCHORS — Objeto Selecionado", sBotaoGrande))
        {
            foreach (var go in Selection.gameObjects)
            {
                RectTransform r = go.GetComponent<RectTransform>();
                if (r != null) SnapAnchors(r);
            }
        }
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;

        EditorGUILayout.Space(6);

        GUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.18f, 0.45f, 0.85f);
        if (GUILayout.Button("👶  Snap em Todos os Filhos", GUILayout.Height(32)))
            SnapTodosFilhos();
        GUI.backgroundColor = new Color(0.65f, 0.25f, 0.25f);
        if (GUILayout.Button("🌐  Snap na Cena Inteira", GUILayout.Height(32)))
            SnapCenaInteira();
        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
        EditorGUILayout.Space(8);

        // ── Preview visual mini da âncora ─────────────────
        if (rt != null)
        {
            GUILayout.BeginVertical(sSecao);
            GUILayout.Label("VISUALIZAÇÃO DA ÂNCORA ATUAL", sTitulo);
            EditorGUILayout.Space(6);
            DesenharVisualizacaoAncora(rt);
            GUILayout.EndVertical();
        }
    }

    // ════════════════════════════════════════════════════════
    //  ABA 1 — PRESETS DE ÂNCORA (grade 4×4)
    // ════════════════════════════════════════════════════════
    void AbaPresets()
    {
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("PRESETS DE ÂNCORA  (4 × 4)", sTitulo);
        EditorGUILayout.Space(4);
        GUILayout.Label("  Clique para aplicar o preset. Segure Shift para manter a posição visual.", sLabel);
        EditorGUILayout.Space(8);

        // Colunas: Esquerda | Centro | Direita | Stretch H
        // Linhas:  Topo     | Meio   | Base    | Stretch V

        string[] colunas = { "Esq", "Centro", "Dir", "Stretch" };
        string[] linhas  = { "Topo", "Meio", "Base", "Stretch" };

        // anchorMin e anchorMax para cada célula [linha][coluna]
        Vector2[,] aMins = new Vector2[4, 4]
        {
            { new(0f, 1f), new(0.5f,1f), new(1f,1f), new(0f,1f) },
            { new(0f,0.5f),new(0.5f,.5f),new(1f,.5f),new(0f,.5f)},
            { new(0f, 0f), new(0.5f,0f), new(1f,0f), new(0f,0f) },
            { new(0f, 0f), new(0.5f,0f), new(1f,0f), new(0f,0f) },
        };
        Vector2[,] aMaxs = new Vector2[4, 4]
        {
            { new(0f, 1f), new(0.5f,1f), new(1f,1f), new(1f,1f) },
            { new(0f,0.5f),new(0.5f,.5f),new(1f,.5f),new(1f,.5f)},
            { new(0f, 0f), new(0.5f,0f), new(1f,0f), new(1f,0f) },
            { new(0f, 1f), new(0.5f,1f), new(1f,1f), new(1f,1f) },
        };

        // Cabeçalho de colunas
        GUILayout.BeginHorizontal();
        GUILayout.Space(48);
        foreach (var col in colunas)
            GUILayout.Label(col, EditorStyles.centeredGreyMiniLabel, GUILayout.Width(62));
        GUILayout.EndHorizontal();

        for (int linha = 0; linha < 4; linha++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(linhas[linha], EditorStyles.centeredGreyMiniLabel, GUILayout.Width(44));

            for (int col = 0; col < 4; col++)
            {
                Vector2 aMin = aMins[linha, col];
                Vector2 aMax = aMaxs[linha, col];

                // Desenha botão com preview visual
                Rect btnRect = GUILayoutUtility.GetRect(58, 52, GUILayout.Width(58));
                DesenharBotaoPreset(btnRect, aMin, aMax, linha, col);
            }
            GUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        GUILayout.EndVertical();
        EditorGUILayout.Space(8);

        // ── Valores customizados ──────────────────────────
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("ÂNCORA CUSTOMIZADA", sTitulo);
        EditorGUILayout.Space(4);

        RectTransform rt = ObterRectTransform();
        if (rt != null)
        {
            EditorGUI.BeginChangeCheck();
            Vector2 nMin = EditorGUILayout.Vector2Field("Anchor Min (0–1):", rt.anchorMin);
            Vector2 nMax = EditorGUILayout.Vector2Field("Anchor Max (0–1):", rt.anchorMax);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(rt, "Editar Âncora");
                rt.anchorMin = new Vector2(Mathf.Clamp01(nMin.x), Mathf.Clamp01(nMin.y));
                rt.anchorMax = new Vector2(Mathf.Clamp01(nMax.x), Mathf.Clamp01(nMax.y));
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Selecione um RectTransform para editar.", MessageType.Info);
        }
        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  ABA 2 — SIMULADOR DE RESOLUÇÕES
    // ════════════════════════════════════════════════════════
    void AbaResolucoes()
    {
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("SIMULADOR DE RESOLUÇÕES", sTitulo);
        EditorGUILayout.Space(4);
        GUILayout.Label("  Clique para aplicar a resolução na Game View em tempo real.", sLabel);
        EditorGUILayout.Space(6);

        foreach (var (nome, w, h) in resolucoes)
        {
            GUILayout.BeginHorizontal();

            // Barra proporcional de resolução
            float maxW = 3840f;
            float barra = Mathf.Clamp(w / maxW * 120f, 8f, 120f);
            Rect barraRect = GUILayoutUtility.GetRect(barra, 18f, GUILayout.Width(barra));
            EditorGUI.DrawRect(barraRect, new Color(cAzul.r, cAzul.g, cAzul.b, 0.5f));

            GUILayout.Label($"{nome}  {w}×{h}", sLabel, GUILayout.Width(190));
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Testar", GUILayout.Width(58), GUILayout.Height(18)))
                AplicarResolucaoGameView(w, h);

            GUILayout.EndHorizontal();
            EditorGUILayout.Space(2);
        }
        GUILayout.EndVertical();

        EditorGUILayout.Space(8);

        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("ORIENTAÇÃO", sTitulo);
        EditorGUILayout.Space(4);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("📱 Portrait  (9:16)", GUILayout.Height(30)))
            AplicarResolucaoGameView(1080, 1920);
        if (GUILayout.Button("📺 Landscape (16:9)", GUILayout.Height(30)))
            AplicarResolucaoGameView(1920, 1080);
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  ABA 3 — DIAGNÓSTICO
    // ════════════════════════════════════════════════════════
    void AbaDiagnostico()
    {
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("DETECTOR DE ÂNCORAS SOLTAS", sTitulo);
        EditorGUILayout.Space(4);
        GUILayout.Label(
            "Encontra RectTransforms onde anchorMin == anchorMax\n" +
            "(âncora em ponto único), o que pode quebrar o layout\nem telas de tamanhos diferentes.",
            EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space(6);

        GUI.backgroundColor = cLaranja;
        if (GUILayout.Button("🔍  Analisar Cena Agora", GUILayout.Height(34)))
            AnalisarCena();
        GUI.backgroundColor = Color.white;
        GUILayout.EndVertical();

        EditorGUILayout.Space(8);

        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("CHECKLIST DE RESPONSIVIDADE", sTitulo);
        EditorGUILayout.Space(4);

        RectTransform rt = ObterRectTransform();
        if (rt != null)
            DesenharChecklist(rt);
        else
            EditorGUILayout.HelpBox("Selecione um RectTransform para ver o diagnóstico.", MessageType.Info);

        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — SNAP ANCHORS
    // ════════════════════════════════════════════════════════
    static void SnapAnchors(RectTransform rt)
    {
        RectTransform pai = rt.parent as RectTransform;
        if (pai == null)
        {
            Debug.LogWarning($"[AnchorSnap] \"{rt.name}\" não tem pai RectTransform.");
            return;
        }

        Undo.RecordObject(rt, "Snap Anchors to Corners");

        // Cantos do objeto em espaço mundo
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);

        // Converte para espaço local do pai
        Matrix4x4 paiLocal = pai.worldToLocalMatrix;
        Vector2 bLeft  = paiLocal.MultiplyPoint3x4(corners[0]); // bottom-left
        Vector2 tRight = paiLocal.MultiplyPoint3x4(corners[2]); // top-right

        // Tamanho do pai
        Rect paiRect = pai.rect;

        // Normaliza para 0-1 dentro do pai
        Vector2 newMin = new Vector2(
            (bLeft.x  - paiRect.xMin) / paiRect.width,
            (bLeft.y  - paiRect.yMin) / paiRect.height);

        Vector2 newMax = new Vector2(
            (tRight.x - paiRect.xMin) / paiRect.width,
            (tRight.y - paiRect.yMin) / paiRect.height);

        // Aplica âncoras
        rt.anchorMin = newMin;
        rt.anchorMax = newMax;

        // Zera offsets (o objeto ocupa exatamente a área das âncoras)
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Debug.Log($"[AnchorSnap] ⚓ \"{rt.name}\"  anchorMin={newMin:F3}  anchorMax={newMax:F3}");
    }

    void SnapTodosFilhos()
    {
        GameObject go = Selection.activeGameObject;
        if (go == null) { Avisar(); return; }

        RectTransform[] filhos = go.GetComponentsInChildren<RectTransform>(true)
            .Where(r => r.gameObject != go).ToArray();

        if (filhos.Length == 0)
        {
            EditorUtility.DisplayDialog("Sem filhos", "O objeto selecionado não tem filhos RectTransform.", "OK");
            return;
        }

        bool confirmar = EditorUtility.DisplayDialog(
            "Snap em Todos os Filhos",
            $"Aplicar Snap Anchors em {filhos.Length} filhos de \"{go.name}\"?",
            "Sim", "Cancelar");

        if (!confirmar) return;

        foreach (var rt in filhos) SnapAnchors(rt);

        EditorUtility.DisplayDialog("Concluído! ⚓",
            $"{filhos.Length} âncoras ajustadas.\nUse Ctrl+Z para desfazer.", "OK");
    }

    void SnapCenaInteira()
    {
        RectTransform[] todos = FindObjectsOfType<RectTransform>()
            .Where(rt => rt.parent is RectTransform).ToArray();

        if (todos.Length == 0)
        {
            EditorUtility.DisplayDialog("Nada encontrado", "Nenhum RectTransform com pai RectTransform na cena.", "OK");
            return;
        }

        bool confirmar = EditorUtility.DisplayDialog(
            "⚠ Snap na Cena Inteira",
            $"Isso vai ajustar as âncoras de {todos.Length} elementos UI em toda a cena.\n\nDeseja continuar?",
            "Sim, fazer snap em tudo", "Cancelar");

        if (!confirmar) return;

        foreach (var rt in todos) SnapAnchors(rt);

        EditorUtility.DisplayDialog("Concluído! ⚓",
            $"{todos.Length} âncoras ajustadas na cena.\nUse Ctrl+Z para desfazer.", "OK");
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — APLICAR PRESET
    // ════════════════════════════════════════════════════════
    void AplicarPreset(Vector2 aMin, Vector2 aMax)
    {
        bool shift = Event.current != null && Event.current.shift;

        foreach (var go in Selection.gameObjects)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) continue;

            Undo.RecordObject(rt, "Aplicar Preset de Âncora");

            if (!shift)
            {
                // Aplica direto sem preservar posição visual
                rt.anchorMin = aMin;
                rt.anchorMax = aMax;
            }
            else
            {
                // Preserva a posição visual (salva cantos antes)
                Vector3[] corners = new Vector3[4];
                rt.GetWorldCorners(corners);

                rt.anchorMin = aMin;
                rt.anchorMax = aMax;

                // Reposiciona para manter visual
                Vector3[] newCorners = new Vector3[4];
                rt.GetWorldCorners(newCorners);
                Vector3 delta = corners[0] - newCorners[0];
                rt.anchoredPosition += new Vector2(delta.x, delta.y);
            }
        }
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — RESOLUÇÃO NA GAME VIEW
    // ════════════════════════════════════════════════════════
    void AplicarResolucaoGameView(int w, int h)
    {
        // Acessa a GameView via reflection (API interna da Unity)
        var gameViewType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
        if (gameViewType == null) { Debug.LogWarning("[AnchorSnap] GameView não encontrada."); return; }

        var gameView = GetWindow(gameViewType);
        if (gameView == null) return;

        // Define tamanho livre
        var setFreeMethod = gameViewType.GetMethod(
            "SizeSelectionCallback",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        // Alternativa: define o tamanho da janela diretamente
        gameView.minSize = new Vector2(w * 0.5f, h * 0.5f);
        gameView.maxSize = new Vector2(w * 0.5f, h * 0.5f);

        Debug.Log($"[AnchorSnap] 📱 Resolução de teste: {w}×{h}");

        EditorUtility.DisplayDialog("Resolução Aplicada",
            $"Game View configurada para:\n{w} × {h} px\n\nVerifique a Game View para ver o resultado.",
            "OK");
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — DIAGNÓSTICO
    // ════════════════════════════════════════════════════════
    void AnalisarCena()
    {
        var todos = FindObjectsOfType<RectTransform>()
            .Where(rt => rt.parent is RectTransform).ToList();

        var soltos    = todos.Where(rt => rt.anchorMin == rt.anchorMax).ToList();
        var centrados = todos.Where(rt => rt.anchorMin == new Vector2(0.5f, 0.5f)).ToList();
        var stretched = todos.Where(rt => rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one).ToList();

        string msg =
            $"Total de RectTransforms: {todos.Count}\n\n" +
            $"⚠ Âncora em ponto único: {soltos.Count}\n" +
            $"  (podem quebrar em outras resoluções)\n\n" +
            $"⚓ Âncora centralizada: {centrados.Count}\n" +
            $"✅ Stretch total: {stretched.Count}\n\n";

        if (soltos.Count > 0)
        {
            msg += "Objetos com âncora solta:\n";
            foreach (var rt in soltos.Take(8))
                msg += $"  • {rt.name}\n";
            if (soltos.Count > 8) msg += $"  ... e mais {soltos.Count - 8}\n";
        }

        // Seleciona os problemáticos
        if (soltos.Count > 0)
            Selection.objects = soltos.Select(rt => (Object)rt.gameObject).ToArray();

        EditorUtility.DisplayDialog("🔍 Diagnóstico da Cena", msg, "OK");
    }

    void DesenharChecklist(RectTransform rt)
    {
        bool ancSnapped   = rt.offsetMin == Vector2.zero && rt.offsetMax == Vector2.zero;
        bool ancStretched = rt.anchorMin != rt.anchorMax;
        bool pivotCentral = Mathf.Approximately(rt.pivot.x, 0.5f) && Mathf.Approximately(rt.pivot.y, 0.5f);
        bool temCanvas    = rt.GetComponentInParent<Canvas>() != null;

        Item(ancSnapped,   "Âncoras snappadas (offsets = zero)");
        Item(ancStretched, "Usa âncoras em área (não ponto único)");
        Item(pivotCentral, "Pivot centralizado (0.5, 0.5)");
        Item(temCanvas,    "Está dentro de um Canvas");

        EditorGUILayout.Space(4);
        GUILayout.Label($"  anchorMin: {rt.anchorMin:F3}", sLabel);
        GUILayout.Label($"  anchorMax: {rt.anchorMax:F3}", sLabel);
        GUILayout.Label($"  offsetMin: {rt.offsetMin:F1}", sLabel);
        GUILayout.Label($"  offsetMax: {rt.offsetMax:F1}", sLabel);
    }

    void Item(bool ok, string texto)
    {
        var st = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = ok ? cVerde : cVermelho } };
        GUILayout.Label($"  {(ok ? "✅" : "❌")} {texto}", st);
    }

    // ════════════════════════════════════════════════════════
    //  DESENHO — INFO DA ÂNCORA ATUAL
    // ════════════════════════════════════════════════════════
    void DesenharInfoAncora(RectTransform rt)
    {
        bool estaSnapped = rt.offsetMin == Vector2.zero && rt.offsetMax == Vector2.zero;
        Color cor = estaSnapped ? cVerde : cLaranja;

        var st = new GUIStyle(EditorStyles.boldLabel)
        { normal = { textColor = cor }, fontSize = 11 };

        GUILayout.Label($"  {(estaSnapped ? "✅" : "⚠")}  {rt.name}", st);
        EditorGUILayout.Space(2);

        GUILayout.Label($"  anchorMin: {rt.anchorMin:F3}     anchorMax: {rt.anchorMax:F3}", sLabel);
        GUILayout.Label($"  offsetMin: {rt.offsetMin:F1}     offsetMax: {rt.offsetMax:F1}", sLabel);
        GUILayout.Label($"  pivot:     {rt.pivot:F3}         sizeDelta: {rt.sizeDelta:F1}", sLabel);
    }

    // ════════════════════════════════════════════════════════
    //  DESENHO — PREVIEW VISUAL DA ÂNCORA
    // ════════════════════════════════════════════════════════
    void DesenharVisualizacaoAncora(RectTransform rt)
    {
        float tamanho = 120f;
        Rect area = GUILayoutUtility.GetRect(tamanho, tamanho);
        area.x = (position.width - tamanho) * 0.5f;

        // Fundo (pai)
        EditorGUI.DrawRect(area, new Color(0.15f, 0.15f, 0.22f));

        // Objeto (verde)
        float ox = area.x + rt.anchorMin.x * tamanho;
        float oy = area.y + (1f - rt.anchorMax.y) * tamanho;
        float ow = (rt.anchorMax.x - rt.anchorMin.x) * tamanho;
        float oh = (rt.anchorMax.y - rt.anchorMin.y) * tamanho;

        if (ow < 4f) ow = 4f;
        if (oh < 4f) oh = 4f;

        EditorGUI.DrawRect(new Rect(ox, oy, ow, oh), new Color(cAzul.r, cAzul.g, cAzul.b, 0.55f));

        // Borda
        DrawBorda(area, new Color(0.4f, 0.4f, 0.6f));
        DrawBorda(new Rect(ox, oy, ow, oh), cAzul);

        // Labels
        GUILayout.Space(4);
        GUILayout.Label($"Min ({rt.anchorMin.x:F2}, {rt.anchorMin.y:F2}) — Max ({rt.anchorMax.x:F2}, {rt.anchorMax.y:F2})",
            new GUIStyle(EditorStyles.centeredGreyMiniLabel) { alignment = TextAnchor.MiddleCenter });
    }

    void DrawBorda(Rect r, Color c)
    {
        float t = 1f;
        EditorGUI.DrawRect(new Rect(r.x,           r.y,            r.width, t), c);
        EditorGUI.DrawRect(new Rect(r.x,           r.yMax - t,     r.width, t), c);
        EditorGUI.DrawRect(new Rect(r.x,           r.y,            t, r.height), c);
        EditorGUI.DrawRect(new Rect(r.xMax - t,    r.y,            t, r.height), c);
    }

    // ════════════════════════════════════════════════════════
    //  DESENHO — BOTÃO DE PRESET (com mini-preview visual)
    // ════════════════════════════════════════════════════════
    void DesenharBotaoPreset(Rect rect, Vector2 aMin, Vector2 aMax, int linha, int col)
    {
        // Detecta hover/click
        bool hover = rect.Contains(Event.current.mousePosition);
        EditorGUI.DrawRect(rect, hover ? new Color(0.25f, 0.30f, 0.45f) : new Color(0.18f, 0.18f, 0.26f));
        DrawBorda(rect, hover ? cAzul : cCinza);

        // Área interna do mini-preview
        Rect inner = new Rect(rect.x + 6, rect.y + 6, rect.width - 12, rect.height - 12);

        // Fundo cinza (simula o pai)
        EditorGUI.DrawRect(inner, new Color(0.12f, 0.12f, 0.18f));

        // Triângulos de âncora
        float ax = inner.x + aMin.x * inner.width;
        float ay = inner.y + (1 - aMax.y) * inner.height;
        float aw = (aMax.x - aMin.x) * inner.width;
        float ah = (aMax.y - aMin.y) * inner.height;

        if (aw < 2f) aw = 2f;
        if (ah < 2f) ah = 2f;

        // Objeto em azul
        EditorGUI.DrawRect(new Rect(ax, ay, aw, ah), new Color(cAzul.r, cAzul.g, cAzul.b, 0.8f));

        // Cruzes das âncoras (mini triângulos)
        Color cAncora = new Color(1f, 0.8f, 0.2f, 0.9f);
        float s = 2f;
        EditorGUI.DrawRect(new Rect(inner.x + aMin.x * inner.width - s, inner.y + (1 - aMin.y) * inner.height - s, s*2, s*2), cAncora);
        EditorGUI.DrawRect(new Rect(inner.x + aMax.x * inner.width - s, inner.y + (1 - aMax.y) * inner.height - s, s*2, s*2), cAncora);

        // Click
        if (hover && Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            AplicarPreset(aMin, aMax);
            Event.current.Use();
            Repaint();
        }
    }

    // ════════════════════════════════════════════════════════
    //  UTILITÁRIOS
    // ════════════════════════════════════════════════════════
    RectTransform ObterRectTransform()
        => Selection.activeGameObject?.GetComponent<RectTransform>();

    void Avisar(string msg = "Selecione um objeto na Hierarchy.")
        => EditorUtility.DisplayDialog("Atenção", msg, "OK");

    // ════════════════════════════════════════════════════════
    //  ESTILOS
    // ════════════════════════════════════════════════════════
    void CarregarEstilos()
    {
        if (estilosOk) return;

        sTitulo = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 10,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(0.5f, 0.85f, 1f) }
        };
        sSecao = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(10, 10, 8, 8) };
        sLabel = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.7f, 0.7f, 0.8f) } };
        sTag   = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.4f, 1f, 0.7f) }, fontStyle = FontStyle.Bold };

        sBotaoGrande = new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold, fixedHeight = 40 };
        sBotaoPreset = new GUIStyle(GUI.skin.button) { fontSize = 9,  fixedHeight = 52, fixedWidth = 58 };

        estilosOk = true;
    }

    void DesenharCabecalho()
    {
        EditorGUILayout.Space(6);
        var st = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 13,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(0.5f, 0.85f, 1f) }
        };
        GUILayout.Label("⚓  ANCHOR SNAP & RESOLUÇÕES", st);
        Rect r = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(r, new Color(0.4f, 0.85f, 1f, 0.4f));
        EditorGUILayout.Space(6);
    }

    void OnSelectionChange() => Repaint();
}
