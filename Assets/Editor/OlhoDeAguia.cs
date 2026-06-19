using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

// ============================================================
// OlhoDeAguia.cs  —  Ferramenta de Editor
//
// Abre em: Ferramentas → 🦅 Olho de Águia — Objetos Perdidos
//          Atalho: Ctrl + Shift + O
//
// Detecta objetos "fantasmas" que causam problemas:
//
//   ⚖  Escala zero (0,0,0) ou qualquer eixo zerado
//   📍  Coordenadas absurdas (além do limiar configurado)
//   🔢  Posição NaN ou Infinity (objeto degenerado)
//   👻  UI com CanvasGroup.alpha = 0 mas rodando scripts
//   🎨  SpriteRenderer com cor.alpha = 0 (invisível ativo)
//   📦  Renderer desativado mas GameObject ainda ativo
//   🖼  RectTransform fora dos limites do Canvas
//   🖱  UI Invisível sobre botões (bloqueio de cliques)
//
// Cada resultado tem: Selecionar | Focar na Cena | Fix Rápido | Deletar
// Política de Log: SOMENTE Debug.LogWarning — nunca LogError.
// ============================================================

public class OlhoDeAguia : EditorWindow
{
    // ── Estrutura de resultado ───────────────────────────────
    private enum Categoria
    {
        EscalaZero,
        CoordAbsurda,
        NaNInfinity,
        CanvasGroupZero,
        SpriteAlphaZero,
        RendererDesativado,
        UIForaDoCanvas,
        UIInvisivelAtiva,
    }

    private class Achado
    {
        public GameObject go;
        public Categoria  categoria;
        public string     titulo;
        public string     detalhe;
        public Vector3    posicao;
        public bool       marcado   = true;
        public System.Action acaoFix;
        public string         labelFix;
    }

    // ── Estado ───────────────────────────────────────────────
    private List<Achado> achados       = new List<Achado>();
    private bool         escaneou      = false;
    private bool         escaneando    = false;
    private float        progresso     = 0f;
    private string       statusAtual   = "";
    private Vector2      scroll;
    private bool         estilosOk     = false;

    // Configurações
    private float  limiarDistancia = 5000f;
    private bool   incluirInativos = true;
    private bool   chkEscala       = true;
    private bool   chkCoord        = true;
    private bool   chkNaN          = true;
    private bool   chkCanvasGroup  = true;
    private bool   chkSprite       = true;
    private bool   chkRenderer     = true;
    private bool   chkUICanvas     = true;

    // Filtros de exibição
    private bool mostrarCritico = true;
    private bool mostrarAviso   = true;

    // Contadores
    private int totalCritico, totalAviso;

    // Estilos
    private GUIStyle sTitulo, sSecao, sLabel, sBotaoGrande, sBotaoItem, sCoord;

    // Cores
    private static readonly Color cAzul    = new Color(0.20f, 0.55f, 1.00f);
    private static readonly Color cVerde   = new Color(0.15f, 0.75f, 0.40f);
    private static readonly Color cLaranja = new Color(1.00f, 0.55f, 0.10f);
    private static readonly Color cVermel  = new Color(0.90f, 0.22f, 0.20f);
    private static readonly Color cAmarelo = new Color(0.95f, 0.85f, 0.10f);
    private static readonly Color cRoxo    = new Color(0.65f, 0.25f, 1.00f);
    private static readonly Color cCinza   = new Color(0.40f, 0.40f, 0.50f);

    // Ícone por categoria
    private static readonly Dictionary<Categoria, string> Icones =
        new Dictionary<Categoria, string>
        {
            { Categoria.EscalaZero,        "⚖" },
            { Categoria.CoordAbsurda,      "📍" },
            { Categoria.NaNInfinity,       "🔢" },
            { Categoria.CanvasGroupZero,   "👻" },
            { Categoria.SpriteAlphaZero,   "🎨" },
            { Categoria.RendererDesativado,"📦" },
            { Categoria.UIForaDoCanvas,    "🖼" },
            { Categoria.UIInvisivelAtiva,  "🖱" },
        };

    // Título por categoria
    private static readonly Dictionary<Categoria, string> Titulos =
        new Dictionary<Categoria, string>
        {
            { Categoria.EscalaZero,        "⚖  Escala Zero / Zerada" },
            { Categoria.CoordAbsurda,      "📍  Coordenadas Absurdas" },
            { Categoria.NaNInfinity,       "🔢  Posição NaN / Infinity" },
            { Categoria.CanvasGroupZero,   "👻  UI — CanvasGroup Alpha 0" },
            { Categoria.SpriteAlphaZero,   "🎨  Sprite Invisível (Alpha 0)" },
            { Categoria.RendererDesativado,"📦  Renderer Desativado" },
            { Categoria.UIForaDoCanvas,    "🖼  UI Fora do Canvas" },
            { Categoria.UIInvisivelAtiva,  "🖱  UI Invisível Sobre Outros (Bloqueio)" },
        };

    // ════════════════════════════════════════════════════════
    //  ABRIR
    // ════════════════════════════════════════════════════════
    [MenuItem("Ferramentas/🦅 Olho de Águia — Objetos Perdidos  %#o")]
    public static void Abrir()
    {
        var w = GetWindow<OlhoDeAguia>("🦅 Olho de Águia");
        w.minSize = new Vector2(370, 580);
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
        DesenharPainelConfig();
        EditorGUILayout.Space(8);

        if (escaneando)
        {
            DesenharProgresso();
        }
        else if (escaneou)
        {
            DesenharResumo();
            EditorGUILayout.Space(6);
            DesenharFiltros();
            EditorGUILayout.Space(6);
            DesenharResultados();
        }
        else
        {
            DesenharLegenda();
        }

        GUILayout.EndScrollView();
    }

    // ════════════════════════════════════════════════════════
    //  CABEÇALHO
    // ════════════════════════════════════════════════════════
    void DesenharCabecalho()
    {
        EditorGUILayout.Space(6);
        var stTitle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 14,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(0.5f, 0.85f, 1f) }
        };
        GUILayout.Label("🦅  OLHO DE ÁGUIA", stTitle);
        var stSub = new GUIStyle(EditorStyles.centeredGreyMiniLabel) { fontSize = 9 };
        GUILayout.Label("Localizador de Objetos Invisíveis, Perdidos e Fantasmas", stSub);
        Rect r = EditorGUILayout.GetControlRect(false, 2);
        EditorGUI.DrawRect(r, new Color(0.4f, 0.85f, 1f, 0.4f));
        EditorGUILayout.Space(6);
    }

    // ════════════════════════════════════════════════════════
    //  PAINEL DE CONFIGURAÇÃO
    // ════════════════════════════════════════════════════════
    void DesenharPainelConfig()
    {
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("CONFIGURAÇÃO DA VARREDURA", sTitulo);
        EditorGUILayout.Space(6);

        // Limiar de distância
        GUILayout.BeginHorizontal();
        GUILayout.Label("📏 Distância máxima aceitável:", sLabel, GUILayout.Width(200));
        limiarDistancia = EditorGUILayout.FloatField(limiarDistancia, GUILayout.Width(70));
        GUILayout.Label("unidades", sLabel);
        GUILayout.EndHorizontal();

        incluirInativos = EditorGUILayout.Toggle("  Incluir objetos inativos:", incluirInativos);
        EditorGUILayout.Space(6);

        // Checkboxes do que verificar (2 colunas)
        GUILayout.Label("Verificar:", sLabel);
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        chkEscala      = GUILayout.Toggle(chkEscala,      "⚖ Escala zero");
        chkCoord       = GUILayout.Toggle(chkCoord,       "📍 Coordenadas absurdas");
        chkNaN         = GUILayout.Toggle(chkNaN,         "🔢 NaN / Infinity");
        chkCanvasGroup = GUILayout.Toggle(chkCanvasGroup, "👻 CanvasGroup alpha 0");
        GUILayout.EndVertical();
        GUILayout.BeginVertical();
        chkSprite   = GUILayout.Toggle(chkSprite,   "🎨 Sprite alpha 0");
        chkRenderer = GUILayout.Toggle(chkRenderer, "📦 Renderer desativado");
        chkUICanvas = GUILayout.Toggle(chkUICanvas, "🖼 UI fora do canvas");
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();

        EditorGUILayout.Space(8);

        // Botão principal
        GUI.backgroundColor = cAzul;
        if (GUILayout.Button("🦅  LOCALIZAR INVISÍVEIS / PERDIDOS", sBotaoGrande))
            Escanear();
        GUI.backgroundColor = Color.white;

        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  RESUMO
    // ════════════════════════════════════════════════════════
    void DesenharResumo()
    {
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("RESULTADO", sTitulo);
        EditorGUILayout.Space(6);

        if (achados.Count == 0)
        {
            var stOk = new GUIStyle(EditorStyles.boldLabel)
            { normal = { textColor = cVerde }, fontSize = 12, alignment = TextAnchor.MiddleCenter };
            GUILayout.Label("✅  Nenhum objeto perdido ou invisível encontrado!\n     Cena está limpa.", stOk);
        }
        else
        {
            string stGeral = totalCritico > 0
                ? $"❌  {totalCritico} objeto(s) crítico(s)  +  ⚠ {totalAviso} aviso(s)"
                : $"⚠  {totalAviso} aviso(s) encontrado(s)";
            Color corGeral = totalCritico > 0 ? cVermel : cLaranja;

            GUILayout.Label(stGeral, new GUIStyle(EditorStyles.boldLabel)
            { normal = { textColor = corGeral }, fontSize = 11 });

            EditorGUILayout.Space(6);

            // Ações em lote
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("✅ Marcar Todos",    GUILayout.Height(22))) achados.ForEach(a => a.marcado = true);
            if (GUILayout.Button("☐ Desmarcar",       GUILayout.Height(22))) achados.ForEach(a => a.marcado = false);
            if (GUILayout.Button("👁 Selec. na Cena", GUILayout.Height(22)))
                Selection.objects = achados.Where(a => a.go != null).Select(a => (Object)a.go).ToArray();
            GUILayout.EndHorizontal();

            EditorGUILayout.Space(4);

            int marcados = achados.Count(a => a.marcado);
            GUI.enabled = marcados > 0;
            GUI.backgroundColor = cVermel;
            if (GUILayout.Button($"🗑  Deletar {marcados} Objeto(s) Marcado(s)", GUILayout.Height(30)))
                DeletarMarcados();
            GUI.backgroundColor = Color.white;
            GUI.enabled = true;
        }

        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  FILTROS
    // ════════════════════════════════════════════════════════
    void DesenharFiltros()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("Mostrar:", sLabel, GUILayout.Width(60));
        GUI.backgroundColor = mostrarCritico ? cVermel : cCinza;
        if (GUILayout.Button($"❌ Críticos ({totalCritico})", GUILayout.Height(22)))
            mostrarCritico = !mostrarCritico;
        GUI.backgroundColor = mostrarAviso ? cLaranja : cCinza;
        if (GUILayout.Button($"⚠ Avisos ({totalAviso})", GUILayout.Height(22)))
            mostrarAviso = !mostrarAviso;
        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();
    }

    // ════════════════════════════════════════════════════════
    //  LISTA DE RESULTADOS (agrupada por categoria)
    // ════════════════════════════════════════════════════════
    void DesenharResultados()
    {
        var categorias = achados
            .Select(a => a.categoria)
            .Distinct()
            .OrderBy(c => (int)c);

        foreach (Categoria cat in categorias)
        {
            var grupo = achados.Where(a => a.categoria == cat).ToList();
            bool eCritico = cat == Categoria.EscalaZero   ||
                            cat == Categoria.NaNInfinity   ||
                            cat == Categoria.CoordAbsurda  ||
                            cat == Categoria.CanvasGroupZero;

            if (eCritico  && !mostrarCritico) continue;
            if (!eCritico && !mostrarAviso)   continue;

            // Cabeçalho da categoria
            EditorGUILayout.Space(6);
            Color corCat = eCritico ? cVermel : cLaranja;
            var stCat = new GUIStyle(EditorStyles.boldLabel)
            { fontSize = 10, normal = { textColor = corCat } };
            GUILayout.Label($"{Titulos[cat]}  ({grupo.Count})", stCat);
            Rect lr = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(lr, new Color(corCat.r, corCat.g, corCat.b, 0.3f));

            foreach (var achado in grupo)
                DesenharItemAchado(achado, eCritico);
        }
        EditorGUILayout.Space(10);
    }

    // ════════════════════════════════════════════════════════
    //  ITEM DE RESULTADO
    // ════════════════════════════════════════════════════════
    void DesenharItemAchado(Achado a, bool critico)
    {
        if (a.go == null) return;

        Color corBorda = critico ? cVermel : cLaranja;
        Color corFundo = critico
            ? new Color(0.18f, 0.05f, 0.05f)
            : new Color(0.18f, 0.12f, 0.03f);

        bool  temFix  = a.acaoFix != null;
        float altura  = 28f + (!string.IsNullOrEmpty(a.detalhe) ? 16f : 0f) + 26f + (temFix ? 20f : 0f);

        Rect r = GUILayoutUtility.GetRect(0, altura, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(r, corFundo);
        EditorGUI.DrawRect(new Rect(r.x, r.y, 4, r.height), corBorda);

        // Checkbox
        Rect chkR = new Rect(r.x + 7, r.y + 6, 16, 16);
        a.marcado = EditorGUI.Toggle(chkR, a.marcado);

        // Ícone categoria
        string icone = Icones.ContainsKey(a.categoria) ? Icones[a.categoria] : "•";
        var stIcon = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
        GUI.Label(new Rect(r.x + 26, r.y + 5, 22, 20), icone, stIcon);

        // Nome do objeto
        var stNome = new GUIStyle(EditorStyles.boldLabel)
        { fontSize = 10, normal = { textColor = Color.white } };
        GUI.Label(new Rect(r.x + 50, r.y + 6, r.width - 140, 16), a.go.name, stNome);

        // Posição (coordenadas em cor destacada)
        string coordTxt = $"({a.posicao.x:F0}, {a.posicao.y:F0}, {a.posicao.z:F0})";
        GUI.Label(new Rect(r.x + 50, r.y + 6, r.width - 50, 16),
            coordTxt,
            new GUIStyle(EditorStyles.miniLabel)
            { alignment = TextAnchor.MiddleRight, normal = { textColor = cAmarelo } });

        float y = r.y + 24;

        // Detalhe
        if (!string.IsNullOrEmpty(a.detalhe))
        {
            GUI.Label(new Rect(r.x + 50, y, r.width - 60, 18),
                a.detalhe,
                new GUIStyle(EditorStyles.miniLabel)
                { wordWrap = true, normal = { textColor = new Color(0.70f, 0.70f, 0.80f) } });
            y += 18;
        }

        // Botões de ação em linha
        float btnX = r.x + 50;
        float btnY = y + 2;
        float btnH = 18;

        // Selecionar
        if (GUI.Button(new Rect(btnX, btnY, 65, btnH), "Selecionar", EditorStyles.miniButton))
        {
            Selection.activeGameObject = a.go;
            EditorGUIUtility.PingObject(a.go);
        }
        btnX += 68;

        // Focar na Cena
        if (GUI.Button(new Rect(btnX, btnY, 78, btnH), "📷 Focar Cena", EditorStyles.miniButton))
        {
            Selection.activeGameObject = a.go;
            SceneView sv = SceneView.lastActiveSceneView;
            if (sv != null) { sv.FrameSelected(); sv.Repaint(); }
        }
        btnX += 81;

        // Fix rápido
        if (temFix)
        {
            if (GUI.Button(new Rect(btnX, btnY, 75, btnH),
                a.labelFix ?? "🔧 Fix", EditorStyles.miniButton))
            {
                Undo.RecordObject(a.go.transform, "OlhoDeAguia — Fix");
                a.acaoFix?.Invoke();
                Escanear(); // re-escaneia após o fix
            }
            btnX += 78;
        }

        // Deletar
        if (GUI.Button(new Rect(btnX, btnY, 50, btnH), "🗑 Del", EditorStyles.miniButton))
        {
            if (EditorUtility.DisplayDialog("Deletar objeto?",
                $"Deletar \"{a.go.name}\"?\n\nUse Ctrl+Z para desfazer.", "Sim", "Não"))
            {
                Undo.DestroyObjectImmediate(a.go);
                achados.Remove(a);
                Repaint();
                return;
            }
        }

        EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), new Color(0.22f, 0.22f, 0.30f));
    }

    // ════════════════════════════════════════════════════════
    //  BARRA DE PROGRESSO
    // ════════════════════════════════════════════════════════
    void DesenharProgresso()
    {
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("ESCANEANDO A CENA...", sTitulo);
        EditorGUILayout.Space(6);
        EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 20), progresso, statusAtual);
        EditorGUILayout.Space(4);
        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  LEGENDA INICIAL
    // ════════════════════════════════════════════════════════
    void DesenharLegenda()
    {
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("O QUE O OLHO DE ÁGUIA DETECTA?", sTitulo);
        EditorGUILayout.Space(8);

        var itens = new[]
        {
            ("⚖",  "Escala Zero",             "Objetos com Scale (0,0,0) ou qualquer eixo zerado", true),
            ("📍", "Coordenadas Absurdas",     $"Objetos além de {limiarDistancia:F0} unidades do centro", true),
            ("🔢", "NaN / Infinity",           "Objetos com posição degenerada (corrompida)",      true),
            ("👻", "CanvasGroup Alpha 0",      "UI invisível mas ainda executando código/scripts",  true),
            ("🎨", "Sprite Alpha 0",           "Sprites transparentes mas ativos na cena",          false),
            ("📦", "Renderer Desativado",      "MeshRenderer/SpriteRenderer desligado com GO ativo",false),
            ("🖼", "UI Fora do Canvas",        "RectTransform com posição fora dos limites visíveis",false),
            ("🖱", "Bloqueio de Cliques",      "Painel invisível com Raycast Target bloqueando UI", true),
        };

        foreach (var (icon, nome, desc, critico) in itens)
        {
            Color cor = critico ? new Color(1f, 0.5f, 0.4f) : new Color(1f, 0.75f, 0.3f);
            GUILayout.BeginHorizontal();
            GUI.color = cor;
            GUILayout.Label(icon, new GUIStyle(EditorStyles.boldLabel) { fontSize = 15 }, GUILayout.Width(22));
            GUI.color = Color.white;
            GUILayout.BeginVertical();
            GUILayout.Label(nome, new GUIStyle(EditorStyles.boldLabel)
            { fontSize = 10, normal = { textColor = cor } });
            GUILayout.Label(desc, sLabel);
            GUILayout.EndVertical();
            GUILayout.Label(critico ? "🔴" : "🟡", GUILayout.Width(20));
            GUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        EditorGUILayout.Space(4);
        GUILayout.Label("  🔴 Crítico   🟡 Aviso", new GUIStyle(EditorStyles.centeredGreyMiniLabel));

        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA PRINCIPAL — ESCANEAR
    // ════════════════════════════════════════════════════════
    void Escanear()
    {
        achados.Clear();
        escaneando = true;
        escaneou   = false;
        progresso  = 0f;
        totalCritico = totalAviso = 0;
        Repaint();

        try
        {
            GameObject[] todos = FindObjectsOfType<GameObject>(incluirInativos);

            float total = todos.Length;
            int   idx   = 0;

            foreach (GameObject go in todos)
            {
                idx++;
                if (idx % 50 == 0)
                {
                    progresso  = idx / total;
                    statusAtual = $"Analisando {go.name}...";
                    Repaint();
                }

                Vector3 pos   = go.transform.position;
                Vector3 scale = go.transform.localScale;

                // ── 1. Escala zero ────────────────────────
                if (chkEscala && (scale == Vector3.zero ||
                    scale.x == 0f || scale.y == 0f || scale.z == 0f))
                {
                    bool qualquerZero = scale.x == 0f || scale.y == 0f || scale.z == 0f;
                    var fix = go; // closure
                    Adicionar(go, Categoria.EscalaZero,
                        $"Escala zerada: {scale}",
                        scale == Vector3.zero
                            ? "Scale total = (0,0,0) — objeto completamente invisível."
                            : $"Eixo zerado em Scale {scale}. O objeto pode estar colapsado.",
                        pos,
                        () => fix.transform.localScale = Vector3.one,
                        "⚖ Resetar Scale (1,1,1)");
                }

                // ── 2. Posição NaN ou Infinity ────────────
                if (chkNaN && (float.IsNaN(pos.x) || float.IsNaN(pos.y) || float.IsNaN(pos.z) ||
                               float.IsInfinity(pos.x) || float.IsInfinity(pos.y) || float.IsInfinity(pos.z)))
                {
                    var fix = go;
                    Adicionar(go, Categoria.NaNInfinity,
                        "Posição NaN/Infinity — objeto corrompido!",
                        "Este objeto tem posição matemática inválida. Pode causar crash na build.",
                        pos,
                        () => fix.transform.position = Vector3.zero,
                        "📍 Teletransportar para (0,0,0)");
                }

                // ── 3. Coordenadas absurdas ───────────────
                else if (chkCoord && pos.magnitude > limiarDistancia)
                {
                    var fix = go;
                    Adicionar(go, Categoria.CoordAbsurda,
                        $"Objeto a {pos.magnitude:F0} unidades do centro!",
                        $"Posição: ({pos.x:F0}, {pos.y:F0}, {pos.z:F0}). " +
                        $"Limiar configurado: {limiarDistancia:F0} unidades.",
                        pos,
                        () => fix.transform.position = Vector3.zero,
                        "📍 Teletransportar para (0,0,0)");
                }

                // ── 4. CanvasGroup alpha = 0 ──────────────
                if (chkCanvasGroup)
                {
                    var cg = go.GetComponent<CanvasGroup>();
                    if (cg != null && cg.alpha <= 0f && go.activeInHierarchy)
                    {
                        bool bloqueia = cg.blocksRaycasts;
                        var  fixCG = cg;
                        Adicionar(go, bloqueia ? Categoria.UIInvisivelAtiva : Categoria.CanvasGroupZero,
                            $"CanvasGroup.alpha = 0 {(bloqueia ? "— BLOQUEANDO CLIQUES!" : "")}",
                            bloqueia
                                ? "⚠ BlocksRaycasts=true: este painel invisível está interceptando cliques dos botões abaixo!"
                                : "Painel totalmente invisível mas rodando scripts. Pode causar cliques fantasmas.",
                            pos,
                            () => { fixCG.alpha = 1f; fixCG.blocksRaycasts = false; },
                            "👻 Tornar Visível + Desbloquear");
                    }
                }

                // ── 5. SpriteRenderer alpha = 0 ──────────
                if (chkSprite)
                {
                    var sr = go.GetComponent<SpriteRenderer>();
                    if (sr != null && sr.color.a <= 0f && go.activeInHierarchy && sr.enabled)
                    {
                        var fixSR = sr;
                        Adicionar(go, Categoria.SpriteAlphaZero,
                            "SpriteRenderer invisível (cor.alpha = 0)",
                            "O sprite está ativo na cena mas completamente transparente. Gasta processamento sem renderizar nada.",
                            pos,
                            () =>
                            {
                                Color c = fixSR.color;
                                c.a = 1f;
                                fixSR.color = c;
                            },
                            "🎨 Restaurar alpha = 1");
                    }
                }

                // ── 6. Renderer desativado ────────────────
                if (chkRenderer)
                {
                    Renderer rd = go.GetComponent<Renderer>();
                    if (rd != null && !rd.enabled && go.activeInHierarchy)
                    {
                        var fixRD = rd;
                        Adicionar(go, Categoria.RendererDesativado,
                            $"Renderer desativado: {rd.GetType().Name}",
                            "O objeto está ativo na cena mas seu Renderer está desligado. " +
                            "Pode ser intencional ou um erro de configuração.",
                            pos,
                            () => fixRD.enabled = true,
                            "📦 Ativar Renderer");
                    }
                }

                // ── 7. UI fora dos limites do Canvas ──────
                if (chkUICanvas)
                {
                    RectTransform rt = go.GetComponent<RectTransform>();
                    if (rt != null && rt.parent is RectTransform)
                    {
                        Vector2 ap = rt.anchoredPosition;
                        if (Mathf.Abs(ap.x) > 5000f || Mathf.Abs(ap.y) > 5000f)
                        {
                            var fixRT = rt;
                            Adicionar(go, Categoria.UIForaDoCanvas,
                                $"UI fora da tela: anchoredPos ({ap.x:F0}, {ap.y:F0})",
                                "RectTransform com posição muito além dos limites visíveis da tela. " +
                                "Pode ser um elemento que sumiu mas ainda está ativo.",
                                rt.position,
                                () => fixRT.anchoredPosition = Vector2.zero,
                                "🖼 Centralizar (0,0)");
                        }
                    }
                }

                // ── 8. Image com Raycast Target ligado mas alpha 0 ──
                if (chkCanvasGroup)
                {
                    var img = go.GetComponent<UnityEngine.UI.Image>();
                    if (img != null && img.raycastTarget && img.color.a <= 0.01f && go.activeInHierarchy)
                    {
                        var fixImg = img;
                        Adicionar(go, Categoria.UIInvisivelAtiva,
                            "Image invisível bloqueando cliques!",
                            "Image com alpha ≈ 0 e Raycast Target ativado intercepta cliques/toques. " +
                            "Desative Raycast Target ou remova este objeto.",
                            pos,
                            () => fixImg.raycastTarget = false,
                            "🖱 Desativar Raycast Target");
                    }
                }
            }

            // Ordena: críticos primeiro, depois por magnitude de posição
            achados = achados
                .OrderBy(a => a.categoria)
                .ThenByDescending(a => a.posicao.magnitude)
                .ToList();

            totalCritico = achados.Count(a =>
                a.categoria == Categoria.EscalaZero    ||
                a.categoria == Categoria.NaNInfinity   ||
                a.categoria == Categoria.CoordAbsurda  ||
                a.categoria == Categoria.UIInvisivelAtiva ||
                a.categoria == Categoria.CanvasGroupZero);

            totalAviso = achados.Count - totalCritico;

            // ⚠ LogWarning apenas — nunca LogError
            if (achados.Count > 0)
                Debug.LogWarning(
                    $"[🦅 OlhoDeAguia] Varredura: {achados.Count} objeto(s) suspeito(s) — " +
                    $"❌ {totalCritico} críticos | ⚠ {totalAviso} avisos.");
            else
                Debug.LogWarning("[🦅 OlhoDeAguia] ✅ Cena limpa! Nenhum objeto invisível ou perdido.");
        }
        finally
        {
            escaneando = false;
            escaneou   = true;
            Repaint();
        }
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — DELETAR MARCADOS
    // ════════════════════════════════════════════════════════
    void DeletarMarcados()
    {
        var alvo = achados.Where(a => a.marcado && a.go != null).ToList();
        if (alvo.Count == 0) return;

        bool ok = EditorUtility.DisplayDialog(
            "⚠ Deletar Objetos",
            $"Deletar {alvo.Count} objeto(s) encontrado(s)?\n\n" +
            string.Join("\n", alvo.Take(8).Select(a => $"  • {a.go.name}")) +
            (alvo.Count > 8 ? $"\n  ... +{alvo.Count - 8}" : "") +
            "\n\nUse Ctrl+Z para desfazer.",
            "Sim, deletar", "Cancelar");

        if (!ok) return;

        Undo.SetCurrentGroupName("OlhoDeAguia — Deletar Perdidos");
        int grupo = Undo.GetCurrentGroup();

        int count = 0;
        foreach (var a in alvo)
        {
            if (a.go == null) continue;
            Undo.DestroyObjectImmediate(a.go);
            count++;
        }

        Undo.CollapseUndoOperations(grupo);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.LogWarning($"[🦅 OlhoDeAguia] {count} objeto(s) deletado(s). Use Ctrl+Z para desfazer.");
        Escanear();
    }

    // ════════════════════════════════════════════════════════
    //  UTILITÁRIOS
    // ════════════════════════════════════════════════════════
    void Adicionar(GameObject go, Categoria cat, string titulo, string detalhe,
                   Vector3 pos, System.Action fix = null, string labelFix = null)
    {
        achados.Add(new Achado
        {
            go       = go,
            categoria = cat,
            titulo    = titulo,
            detalhe   = detalhe,
            posicao   = pos,
            acaoFix   = fix,
            labelFix  = labelFix,
            marcado   = true,
        });
    }

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
        sSecao  = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(10, 10, 8, 8) };
        sLabel  = new GUIStyle(EditorStyles.miniLabel)
        { normal = { textColor = new Color(0.72f, 0.72f, 0.82f) }, wordWrap = true };
        sBotaoGrande = new GUIStyle(GUI.skin.button)
        { fontSize = 12, fontStyle = FontStyle.Bold, fixedHeight = 42, normal = { textColor = Color.white } };
        sBotaoItem = new GUIStyle(EditorStyles.miniButton);
        sCoord     = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = cAmarelo }, alignment = TextAnchor.MiddleRight };

        estilosOk = true;
    }
}
