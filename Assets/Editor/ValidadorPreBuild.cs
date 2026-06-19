using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using System.IO;

// ============================================================
// ValidadorPreBuild.cs  —  Ferramenta de Editor
//
// Abre em: Ferramentas → 🚀 Validador Pré-Build
//          Atalho: Ctrl + Shift + B
//
// Verificações incluídas:
//   ✈  Cenas no Build Settings
//   🧩  Prefabs com referências quebradas (Missing References)
//   🖼  Texturas acima do limite da plataforma alvo
//   💀  Scripts quebrados (Missing Scripts) na cena ativa
//   🔊  AudioClips sem compressão configurada
//   📦  Prefabs com Missing Scripts
//
// Política de Log: SOMENTE Debug.LogWarning — nunca LogError.
// ============================================================

public class ValidadorPreBuild : EditorWindow
{
    // ── Plataformas alvo ─────────────────────────────────────
    private enum PlataformaAlvo { Mobile_Android_iOS, Desktop_PC_Mac, Console_PS_Xbox, WebGL }
    private PlataformaAlvo plataforma = PlataformaAlvo.Mobile_Android_iOS;

    // Limites de textura por plataforma (px)
    private static readonly Dictionary<PlataformaAlvo, int> limiteTextura =
        new Dictionary<PlataformaAlvo, int>
        {
            { PlataformaAlvo.Mobile_Android_iOS, 1024 },
            { PlataformaAlvo.Desktop_PC_Mac,     4096 },
            { PlataformaAlvo.Console_PS_Xbox,    2048 },
            { PlataformaAlvo.WebGL,              2048 },
        };

    // ── Estado ───────────────────────────────────────────────
    private List<CheckItem>   resultados    = new List<CheckItem>();
    private bool              executou      = false;
    private bool              executando    = false;
    private float             progresso     = 0f;
    private string            statusAtual   = "";
    private Vector2           scroll;
    private bool              estilosOk     = false;

    // Resumo
    private int totalOk, totalAviso, totalErro;

    // Filtros
    private bool mostrarOk     = true;
    private bool mostrarAviso  = true;
    private bool mostrarErro   = true;

    // ── Estrutura de resultado ───────────────────────────────
    private enum NivelCheck { OK, Aviso, Erro }
    private class CheckItem
    {
        public string   categoria;
        public string   titulo;
        public string   detalhe;
        public NivelCheck nivel;
        public string   caminhoAsset;   // para ping / open
        public System.Action acaoRapida;
        public string   labelAcao;
    }

    // ── Estilos ──────────────────────────────────────────────
    private GUIStyle sTitulo, sSecao, sLabel, sBotaoGrande, sBotaoAcao;
    private static readonly Color cVerde   = new Color(0.15f, 0.78f, 0.40f);
    private static readonly Color cLaranja = new Color(1.00f, 0.55f, 0.10f);
    private static readonly Color cVermel  = new Color(0.90f, 0.22f, 0.20f);
    private static readonly Color cAzul    = new Color(0.20f, 0.55f, 1.00f);
    private static readonly Color cCinza   = new Color(0.40f, 0.40f, 0.50f);

    // ════════════════════════════════════════════════════════
    //  ABRIR
    // ════════════════════════════════════════════════════════
    [MenuItem("Ferramentas/🚀 Validador Pré-Build  %#b")]
    public static void Abrir()
    {
        var w = GetWindow<ValidadorPreBuild>("🚀 Pré-Build");
        w.minSize = new Vector2(360, 580);
        w.Show();
    }

    // ════════════════════════════════════════════════════════
    //  OnGUI
    // ════════════════════════════════════════════════════════
    void OnGUI()
    {
        CarregarEstilos();
        DesenharCabecalho();

        scroll = GUILayout.BeginScrollView(scroll);

        DesenharPainelControle();
        EditorGUILayout.Space(8);

        if (executando)
        {
            DesenharProgresso();
        }
        else if (executou)
        {
            DesenharResumo();
            EditorGUILayout.Space(8);
            DesenharFiltros();
            EditorGUILayout.Space(6);
            DesenharResultados();
        }
        else
        {
            DesenharInstrucoes();
        }

        GUILayout.EndScrollView();
    }

    // ════════════════════════════════════════════════════════
    //  PAINEL DE CONTROLE
    // ════════════════════════════════════════════════════════
    void DesenharPainelControle()
    {
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("CONFIGURAÇÃO", sTitulo);
        EditorGUILayout.Space(4);

        plataforma = (PlataformaAlvo)EditorGUILayout.EnumPopup("🎯 Plataforma Alvo:", plataforma);
        GUILayout.Label($"  Limite de textura: {limiteTextura[plataforma]}×{limiteTextura[plataforma]} px", sLabel);

        EditorGUILayout.Space(8);

        GUI.backgroundColor = cAzul;
        if (GUILayout.Button("🚀  VERIFICAR PROJETO AGORA", sBotaoGrande))
            ExecutarValidacao();
        GUI.backgroundColor = Color.white;

        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  RESUMO
    // ════════════════════════════════════════════════════════
    void DesenharResumo()
    {
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("RESULTADO GERAL", sTitulo);
        EditorGUILayout.Space(6);

        // Barra de status global
        string statusGeral;
        Color  corGeral;
        if (totalErro > 0)        { statusGeral = "❌  BUILD EM RISCO — Corrija os erros antes de compilar!"; corGeral = cVermel; }
        else if (totalAviso > 0)  { statusGeral = "⚠  ATENÇÃO — Há avisos que podem causar problemas.";       corGeral = cLaranja; }
        else                      { statusGeral = "✅  PROJETO OK — Seguro para gerar a Build!";               corGeral = cVerde; }

        var stStatus = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 11,
            wordWrap  = true,
            normal    = { textColor = corGeral }
        };
        GUILayout.Label(statusGeral, stStatus);
        EditorGUILayout.Space(6);

        // Contadores
        GUILayout.BeginHorizontal();
        DesenharContador("✅ OK",      totalOk,    cVerde);
        DesenharContador("⚠ Avisos",  totalAviso, cLaranja);
        DesenharContador("❌ Erros",   totalErro,  cVermel);
        GUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // Botão exportar relatório
        if (GUILayout.Button("📋  Exportar Relatório (.txt)", GUILayout.Height(26)))
            ExportarRelatorio();

        GUILayout.EndVertical();
    }

    void DesenharContador(string label, int valor, Color cor)
    {
        GUILayout.BeginVertical(GUILayout.Width(100));
        var stValor = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 22,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = cor }
        };
        GUILayout.Label(valor.ToString(), stValor, GUILayout.Height(32));
        var stLabel = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
        GUILayout.Label(label, stLabel);
        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  FILTROS
    // ════════════════════════════════════════════════════════
    void DesenharFiltros()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("Mostrar:", sLabel, GUILayout.Width(60));

        GUI.backgroundColor = mostrarErro ? cVermel : cCinza;
        if (GUILayout.Button($"❌ Erros ({totalErro})",    GUILayout.Height(22))) mostrarErro   = !mostrarErro;

        GUI.backgroundColor = mostrarAviso ? cLaranja : cCinza;
        if (GUILayout.Button($"⚠ Avisos ({totalAviso})",  GUILayout.Height(22))) mostrarAviso  = !mostrarAviso;

        GUI.backgroundColor = mostrarOk ? cVerde : cCinza;
        if (GUILayout.Button($"✅ OK ({totalOk})",         GUILayout.Height(22))) mostrarOk     = !mostrarOk;

        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();
    }

    // ════════════════════════════════════════════════════════
    //  LISTA DE RESULTADOS
    // ════════════════════════════════════════════════════════
    void DesenharResultados()
    {
        string catAtual = "";

        foreach (var item in resultados)
        {
            // Aplica filtro
            if (item.nivel == NivelCheck.OK    && !mostrarOk)    continue;
            if (item.nivel == NivelCheck.Aviso && !mostrarAviso) continue;
            if (item.nivel == NivelCheck.Erro  && !mostrarErro)  continue;

            // Cabeçalho de categoria
            if (item.categoria != catAtual)
            {
                catAtual = item.categoria;
                EditorGUILayout.Space(6);
                var stCat = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 10,
                    normal   = { textColor = new Color(0.5f, 0.85f, 1f) }
                };
                GUILayout.Label($"── {catAtual} ──", stCat);
                Rect lr = EditorGUILayout.GetControlRect(false, 1);
                EditorGUI.DrawRect(lr, new Color(0.4f, 0.85f, 1f, 0.25f));
            }

            // Item
            DesenharItemResultado(item);
        }
        EditorGUILayout.Space(8);
    }

    void DesenharItemResultado(CheckItem item)
    {
        Color cor = item.nivel == NivelCheck.OK    ? cVerde
                  : item.nivel == NivelCheck.Aviso ? cLaranja
                  :                                  cVermel;

        string icone = item.nivel == NivelCheck.OK    ? "✅"
                     : item.nivel == NivelCheck.Aviso ? "⚠"
                     :                                  "❌";

        Color fundoCor = item.nivel == NivelCheck.OK
            ? new Color(0.05f, 0.15f, 0.07f)
            : item.nivel == NivelCheck.Aviso
                ? new Color(0.18f, 0.12f, 0.03f)
                : new Color(0.18f, 0.05f, 0.05f);

        // Calcula altura dinâmica
        bool temDetalhe  = !string.IsNullOrEmpty(item.detalhe);
        bool temAcao     = item.acaoRapida != null;
        float altura     = 24 + (temDetalhe ? 16 : 0) + (temAcao ? 24 : 0) + 4;

        Rect r = GUILayoutUtility.GetRect(0, altura, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(r, fundoCor);
        EditorGUI.DrawRect(new Rect(r.x, r.y, 4, r.height), cor);

        float xTexto = r.x + 12;

        // Título
        var stTitulo = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 10,
            normal   = { textColor = Color.white }
        };
        EditorGUI.LabelField(new Rect(xTexto, r.y + 4, r.width - 80, 16),
            $"{icone}  {item.titulo}", stTitulo);

        // Botão ping
        if (!string.IsNullOrEmpty(item.caminhoAsset))
        {
            if (GUI.Button(new Rect(r.xMax - 46, r.y + 4, 42, 16), "ping", EditorStyles.miniButton))
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(item.caminhoAsset);
                if (asset != null) EditorGUIUtility.PingObject(asset);
            }
        }

        float y = r.y + 22;

        // Detalhe
        if (temDetalhe)
        {
            var stDetalhe = new GUIStyle(EditorStyles.miniLabel)
            { normal = { textColor = new Color(0.70f, 0.70f, 0.80f) }, wordWrap = true };
            EditorGUI.LabelField(new Rect(xTexto, y, r.width - 16, 18), item.detalhe, stDetalhe);
            y += 16;
        }

        // Ação rápida
        if (temAcao)
        {
            if (GUI.Button(new Rect(xTexto, y + 2, 150, 18), item.labelAcao ?? "Corrigir", EditorStyles.miniButton))
                item.acaoRapida?.Invoke();
        }

        // Separador
        EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), new Color(0.22f, 0.22f, 0.30f));
    }

    // ════════════════════════════════════════════════════════
    //  PROGRESSO
    // ════════════════════════════════════════════════════════
    void DesenharProgresso()
    {
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("VERIFICANDO...", sTitulo);
        EditorGUILayout.Space(6);
        EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(false, 20), progresso, statusAtual);
        EditorGUILayout.Space(4);
        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  INSTRUÇÕES
    // ════════════════════════════════════════════════════════
    void DesenharInstrucoes()
    {
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("O QUE SERÁ VERIFICADO?", sTitulo);
        EditorGUILayout.Space(6);

        var checks = new[]
        {
            ("✈",  "Cenas no Build Settings",        "Todas as cenas estão listadas e habilitadas?"),
            ("🧩", "Missing References em Prefabs",  "Campos nulos/quebrados em todos os prefabs do projeto"),
            ("💀", "Missing Scripts na Cena",        "Componentes com scripts deletados/renomeados"),
            ("🖼", "Tamanho de Texturas",            $"Texturas acima de {limiteTextura[plataforma]}px para {plataforma}"),
            ("🔊", "Configuração de AudioClips",     "Clips sem compressão definida (aumentam tamanho da build)"),
            ("📦", "Missing Scripts em Prefabs",     "Prefabs com componentes quebrados no disco"),
        };

        foreach (var (icon, titulo, desc) in checks)
        {
            GUILayout.BeginHorizontal();
            var stIcon = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            GUILayout.Label(icon, stIcon, GUILayout.Width(24));
            GUILayout.BeginVertical();
            GUILayout.Label(titulo, EditorStyles.boldLabel);
            GUILayout.Label(desc, sLabel);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — EXECUTAR TODAS AS VALIDAÇÕES
    // ════════════════════════════════════════════════════════
    void ExecutarValidacao()
    {
        resultados.Clear();
        executando = true;
        executou   = false;
        progresso  = 0f;
        totalOk = totalAviso = totalErro = 0;
        Repaint();

        try
        {
            SetStatus("Verificando cenas no Build Settings...", 0.05f);
            ChecarCenasBuild();

            SetStatus("Verificando missing references em prefabs...", 0.20f);
            ChecarMissingReferencesPrefabs();

            SetStatus("Verificando missing scripts em prefabs...", 0.40f);
            ChecarMissingScriptsPrefabs();

            SetStatus("Verificando missing scripts na cena ativa...", 0.55f);
            ChecarMissingScriptsCena();

            SetStatus("Verificando tamanho das texturas...", 0.70f);
            ChecarTexturas();

            SetStatus("Verificando configurações de áudio...", 0.88f);
            ChecarAudio();

            SetStatus("Concluído!", 1.0f);

            // Contagem final
            totalOk    = resultados.Count(r => r.nivel == NivelCheck.OK);
            totalAviso = resultados.Count(r => r.nivel == NivelCheck.Aviso);
            totalErro  = resultados.Count(r => r.nivel == NivelCheck.Erro);

            // Ordena: Erros → Avisos → OK, por categoria
            resultados = resultados
                .OrderBy(r => r.categoria)
                .ThenBy(r => (int)r.nivel == 2 ? 0 : (int)r.nivel == 1 ? 1 : 2)
                .ToList();

            // ⚠ LogWarning apenas — nunca LogError
            Debug.LogWarning(
                $"[🚀 ValidadorPreBuild] Verificação concluída → " +
                $"✅ {totalOk} OK  |  ⚠ {totalAviso} avisos  |  ❌ {totalErro} erros. " +
                $"Plataforma: {plataforma}.");
        }
        finally
        {
            executando = false;
            executou   = true;
            Repaint();
        }
    }

    void SetStatus(string msg, float p)
    {
        statusAtual = msg;
        progresso   = p;
        Repaint();
    }

    void Adicionar(string cat, string titulo, string detalhe, NivelCheck nivel,
                   string caminho = null, System.Action acao = null, string labelAcao = null)
    {
        resultados.Add(new CheckItem
        {
            categoria    = cat,
            titulo       = titulo,
            detalhe      = detalhe,
            nivel        = nivel,
            caminhoAsset = caminho,
            acaoRapida   = acao,
            labelAcao    = labelAcao,
        });
    }

    // ════════════════════════════════════════════════════════
    //  CHECK 1 — CENAS NO BUILD SETTINGS
    // ════════════════════════════════════════════════════════
    void ChecarCenasBuild()
    {
        const string CAT = "✈  Build Settings";

        EditorBuildSettingsScene[] buildScenes = EditorBuildSettings.scenes;

        if (buildScenes.Length == 0)
        {
            Adicionar(CAT, "Nenhuma cena no Build Settings!",
                "Adicione ao menos 1 cena em File → Build Settings.",
                NivelCheck.Erro, null,
                () => EditorWindow.GetWindow(System.Type.GetType("UnityEditor.BuildPlayerWindow,UnityEditor")),
                "Abrir Build Settings");
            return;
        }

        // Todas as cenas no disco
        string[] todasCenas = AssetDatabase.FindAssets("t:Scene")
            .Select(g => AssetDatabase.GUIDToAssetPath(g)).ToArray();

        int habilitadas = 0, desabilitadas = 0, inexistentes = 0;

        foreach (var bs in buildScenes)
        {
            if (!bs.enabled) { desabilitadas++; continue; }
            if (!File.Exists(bs.path)) { inexistentes++; continue; }
            habilitadas++;
        }

        // Cenas no disco mas NÃO no build
        var pathsBuild = new HashSet<string>(buildScenes.Select(s => s.path));
        var cenasFora  = todasCenas.Where(p => !pathsBuild.Contains(p)).ToList();

        Adicionar(CAT, $"{habilitadas} cena(s) habilitada(s) no Build",
            $"Encontradas {buildScenes.Length} entradas no Build Settings.",
            NivelCheck.OK);

        if (desabilitadas > 0)
            Adicionar(CAT, $"{desabilitadas} cena(s) DESABILITADA(S) no Build",
                "Cenas desabilitadas não entram na build. Verifique se é intencional.",
                NivelCheck.Aviso, null,
                () => EditorWindow.GetWindow(System.Type.GetType("UnityEditor.BuildPlayerWindow,UnityEditor")),
                "Abrir Build Settings");

        if (inexistentes > 0)
            Adicionar(CAT, $"{inexistentes} referência(s) de cena INEXISTENTE(S)",
                "O Build Settings aponta para cenas que não existem mais no disco.",
                NivelCheck.Erro, null,
                () => EditorWindow.GetWindow(System.Type.GetType("UnityEditor.BuildPlayerWindow,UnityEditor")),
                "Abrir Build Settings");

        if (cenasFora.Count > 0)
            Adicionar(CAT, $"{cenasFora.Count} cena(s) NÃO listada(s) no Build",
                "Cenas encontradas no projeto mas ausentes do Build Settings:\n" +
                string.Join(", ", cenasFora.Take(5).Select(Path.GetFileNameWithoutExtension)) +
                (cenasFora.Count > 5 ? $" +{cenasFora.Count - 5} mais" : ""),
                NivelCheck.Aviso);
    }

    // ════════════════════════════════════════════════════════
    //  CHECK 2 — MISSING REFERENCES EM PREFABS
    // ════════════════════════════════════════════════════════
    void ChecarMissingReferencesPrefabs()
    {
        const string CAT = "🧩  Missing References (Prefabs)";

        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        int totalPrefabs = guids.Length, comProblema = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            var componentes = prefab.GetComponentsInChildren<Component>(true);
            foreach (Component comp in componentes)
            {
                if (comp == null) continue;

                SerializedObject so = new SerializedObject(comp);
                SerializedProperty prop = so.GetIterator();

                while (prop.NextVisible(true))
                {
                    if (prop.propertyType == SerializedPropertyType.ObjectReference &&
                        prop.objectReferenceValue == null &&
                        prop.objectReferenceInstanceIDValue != 0)
                    {
                        comProblema++;
                        string nomeArquivo = Path.GetFileNameWithoutExtension(path);
                        Adicionar(CAT,
                            $"Missing Reference: {nomeArquivo} → {comp.GetType().Name}.{prop.name}",
                            $"Prefab: {path}",
                            NivelCheck.Erro,
                            path,
                            () => AssetDatabase.OpenAsset(AssetDatabase.LoadMainAssetAtPath(path)),
                            "Abrir Prefab");
                        break; // 1 aviso por componente para não lotar
                    }
                }
            }
        }

        if (comProblema == 0)
            Adicionar(CAT, $"Todos os {totalPrefabs} prefab(s) sem missing references",
                "Nenhum campo nulo quebrado encontrado nos prefabs.", NivelCheck.OK);
    }

    // ════════════════════════════════════════════════════════
    //  CHECK 3 — MISSING SCRIPTS EM PREFABS
    // ════════════════════════════════════════════════════════
    void ChecarMissingScriptsPrefabs()
    {
        const string CAT = "📦  Missing Scripts (Prefabs)";
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        int comMissing = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab);
            if (count > 0)
            {
                comMissing++;
                string nome = Path.GetFileNameWithoutExtension(path);
                Adicionar(CAT,
                    $"Prefab com {count} script(s) quebrado(s): {nome}",
                    $"Caminho: {path}",
                    NivelCheck.Erro,
                    path,
                    () =>
                    {
                        GameObject obj = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if (obj != null)
                        {
                            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(obj);
                            EditorUtility.SetDirty(obj);
                            AssetDatabase.SaveAssets();
                            Debug.LogWarning($"[🚀 ValidadorPreBuild] Missing scripts removidos de: {path}");
                        }
                    },
                    "Remover Missing Scripts");
            }
        }

        if (comMissing == 0)
            Adicionar(CAT, "Nenhum prefab com missing scripts",
                "Todos os prefabs com scripts válidos.", NivelCheck.OK);
    }

    // ════════════════════════════════════════════════════════
    //  CHECK 4 — MISSING SCRIPTS NA CENA ATIVA
    // ════════════════════════════════════════════════════════
    void ChecarMissingScriptsCena()
    {
        const string CAT = "💀  Missing Scripts (Cena Ativa)";

        var todos = FindObjectsOfType<GameObject>(includeInactive: true);
        int totalMissing = 0;
        var objsComMissing = new List<(GameObject go, int count)>();

        foreach (var go in todos)
        {
            int c = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (c > 0) { objsComMissing.Add((go, c)); totalMissing += c; }
        }

        if (totalMissing == 0)
        {
            Adicionar(CAT, "Nenhum missing script na cena ativa",
                $"{todos.Length} objetos verificados sem scripts quebrados.", NivelCheck.OK);
        }
        else
        {
            foreach (var (go, count) in objsComMissing.Take(20))
            {
                GameObject capturado = go; // closure
                Adicionar(CAT,
                    $"{count} missing script(s): {go.name}",
                    $"Hierarquia: {CaminhoGO(go)}",
                    NivelCheck.Erro,
                    null,
                    () =>
                    {
                        if (capturado == null) return;
                        Undo.RecordObject(capturado, "Remove Missing Scripts");
                        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(capturado);
                        EditorSceneManager.MarkSceneDirty(
                            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                        Selection.activeGameObject = capturado;
                        Debug.LogWarning($"[🚀 ValidadorPreBuild] Missing scripts removidos de: {capturado.name}");
                    },
                    "Remover desta cena");
            }
            if (objsComMissing.Count > 20)
                Adicionar(CAT, $"... e mais {objsComMissing.Count - 20} objeto(s)",
                    "Execute o SceneCleaner para limpeza completa.", NivelCheck.Aviso);
        }
    }

    // ════════════════════════════════════════════════════════
    //  CHECK 5 — TAMANHO DE TEXTURAS
    // ════════════════════════════════════════════════════════
    void ChecarTexturas()
    {
        const string CAT = "🖼  Texturas";
        int limite = limiteTextura[plataforma];

        string[] guids = AssetDatabase.FindAssets("t:Texture2D");
        int ok = 0, problemas = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Packages/")) continue; // ignora packages externos

            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) continue;

            int w = tex.width, h = tex.height;
            bool acimaDeLimite = w > limite || h > limite;

            // Verifica se maxTextureSize está configurado corretamente
            int maxConfigured = ti.maxTextureSize;
            bool potenciaAcima = !IsPowerOfTwo(w) || !IsPowerOfTwo(h);

            if (acimaDeLimite)
            {
                problemas++;
                string nome = Path.GetFileNameWithoutExtension(path);
                Adicionar(CAT,
                    $"Textura acima do limite: {nome} ({w}×{h})",
                    $"Limite para {plataforma}: {limite}px. Caminho: {path}",
                    NivelCheck.Erro,
                    path,
                    () =>
                    {
                        TextureImporter imp = AssetImporter.GetAtPath(path) as TextureImporter;
                        if (imp == null) return;
                        imp.maxTextureSize = limite;
                        AssetDatabase.ImportAsset(path);
                        Debug.LogWarning($"[🚀 ValidadorPreBuild] Textura ajustada para {limite}px: {path}");
                    },
                    $"Ajustar para {limite}px");
            }
            else if (potenciaAcima && ti.textureType == TextureImporterType.Default)
            {
                Adicionar(CAT,
                    $"Textura não é potência de 2: {Path.GetFileNameWithoutExtension(path)} ({w}×{h})",
                    "Texturas NPOT consomem mais memória em algumas plataformas.",
                    NivelCheck.Aviso, path);
            }
            else
            {
                ok++;
            }
        }

        if (problemas == 0)
            Adicionar(CAT, $"{ok + problemas} textura(s) dentro do limite de {limite}px",
                $"Todas as texturas compatíveis com {plataforma}.", NivelCheck.OK);
    }

    bool IsPowerOfTwo(int v) => v > 0 && (v & (v - 1)) == 0;

    // ════════════════════════════════════════════════════════
    //  CHECK 6 — ÁUDIO
    // ════════════════════════════════════════════════════════
    void ChecarAudio()
    {
        const string CAT = "🔊  Áudio";
        string[] guids = AssetDatabase.FindAssets("t:AudioClip");

        int semCompressao = 0, ok = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Packages/")) continue;

            AudioImporter ai = AssetImporter.GetAtPath(path) as AudioImporter;
            if (ai == null) continue;

            AudioImporterSampleSettings settings = ai.defaultSampleSettings;

            if (settings.compressionFormat == AudioCompressionFormat.PCM)
            {
                semCompressao++;
                string nome = Path.GetFileNameWithoutExtension(path);
                Adicionar(CAT,
                    $"AudioClip sem compressão (PCM): {nome}",
                    $"PCM aumenta muito o tamanho da build. Use Vorbis/ADPCM.\nCaminho: {path}",
                    NivelCheck.Aviso,
                    path,
                    () =>
                    {
                        AudioImporter imp = AssetImporter.GetAtPath(path) as AudioImporter;
                        if (imp == null) return;
                        var s = imp.defaultSampleSettings;
                        s.compressionFormat = AudioCompressionFormat.Vorbis;
                        imp.defaultSampleSettings = s;
                        AssetDatabase.ImportAsset(path);
                        Debug.LogWarning($"[🚀 ValidadorPreBuild] Áudio convertido para Vorbis: {path}");
                    },
                    "Converter para Vorbis");
            }
            else ok++;
        }

        if (semCompressao == 0)
            Adicionar(CAT, $"{ok} AudioClip(s) com compressão configurada",
                "Todos os clips de áudio estão otimizados.", NivelCheck.OK);
    }

    // ════════════════════════════════════════════════════════
    //  EXPORTAR RELATÓRIO
    // ════════════════════════════════════════════════════════
    void ExportarRelatorio()
    {
        string pasta  = Path.Combine(Application.dataPath, "../Logs");
        Directory.CreateDirectory(pasta);
        string arquivo = Path.Combine(pasta,
            $"PreBuild_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");

        using (var sw = new StreamWriter(arquivo))
        {
            sw.WriteLine("=================================================");
            sw.WriteLine("  RELATÓRIO PRÉ-BUILD — " + System.DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
            sw.WriteLine($"  Plataforma: {plataforma}");
            sw.WriteLine($"  Cena: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            sw.WriteLine("=================================================");
            sw.WriteLine($"  ✅ OK: {totalOk}   ⚠ Avisos: {totalAviso}   ❌ Erros: {totalErro}");
            sw.WriteLine("=================================================\n");

            string catAtual = "";
            foreach (var item in resultados)
            {
                if (item.categoria != catAtual)
                {
                    catAtual = item.categoria;
                    sw.WriteLine($"\n── {catAtual} ──");
                }
                string nivel = item.nivel == NivelCheck.OK ? "OK " : item.nivel == NivelCheck.Aviso ? "AVS" : "ERR";
                sw.WriteLine($"  [{nivel}] {item.titulo}");
                if (!string.IsNullOrEmpty(item.detalhe))
                    sw.WriteLine($"         {item.detalhe}");
            }
        }

        // ⚠ LogWarning apenas
        Debug.LogWarning($"[🚀 ValidadorPreBuild] Relatório exportado: {arquivo}");
        EditorUtility.RevealInFinder(arquivo);
    }

    // ════════════════════════════════════════════════════════
    //  UTILITÁRIOS
    // ════════════════════════════════════════════════════════
    string CaminhoGO(GameObject go)
    {
        string p = go.name;
        Transform pai = go.transform.parent;
        int d = 0;
        while (pai != null && d++ < 3) { p = pai.name + "/" + p; pai = pai.parent; }
        return pai != null ? ".../" + p : p;
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
        sLabel  = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.72f, 0.72f, 0.82f) }, wordWrap = true };

        sBotaoGrande = new GUIStyle(GUI.skin.button)
        { fontSize = 12, fontStyle = FontStyle.Bold, fixedHeight = 40, normal = { textColor = Color.white } };

        sBotaoAcao = new GUIStyle(GUI.skin.button) { fontSize = 9, fixedHeight = 20 };
        estilosOk  = true;
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
        GUILayout.Label("🚀  VALIDADOR PRÉ-BUILD", st);
        Rect r = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(r, new Color(0.4f, 0.85f, 1f, 0.4f));
        EditorGUILayout.Space(6);
    }
}
