using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using System.Collections.Generic;
using System.Linq;

// ============================================================
// SceneCleaner.cs  —  Ferramenta de Editor
//
// Abre em: Ferramentas → 🗂 Gerenciador de Etiquetas & Faxina
//          Atalho: Ctrl + Shift + F
//
// Três módulos integrados:
//   🏷  Tagueamento em Massa   — aplica Tag e Layer na seleção
//   🧹  Faxina de Cena        — remove vazios + scripts quebrados
//   📊  Relatório              — estatísticas e diagnóstico geral
//
// Política de Log: SOMENTE Debug.LogWarning — nunca LogError.
//   Não polui o console com vermelho, não afeta CI/CD nem git.
// ============================================================

public class SceneCleaner : EditorWindow
{
    // ── Abas ─────────────────────────────────────────────────
    private int    abaAtiva = 0;
    private string[] abas  = { "🏷 Taguear", "🧹 Faxina", "📊 Relatório" };

    // ── Estado — Tagueamento ─────────────────────────────────
    private int    tagIndex   = 0;
    private int    layerIndex = 0;
    private bool   aplicarFilhos  = false;
    private bool   novaTagModo    = false;
    private string novaTagNome    = "";

    // ── Estado — Faxina ──────────────────────────────────────
    private List<ResultadoItem> resultadosVazios   = new List<ResultadoItem>();
    private List<ResultadoItem> resultadosMissing  = new List<ResultadoItem>();
    private bool   escaneou         = false;
    private bool   mostrarVazios    = true;
    private bool   mostrarMissing   = true;
    private Vector2 scrollFaxina;

    // ── Estado — Relatório ───────────────────────────────────
    private RelatorioData relatorio;
    private Vector2 scrollRelatorio;

    // ── Estilos ──────────────────────────────────────────────
    private bool estilosOk = false;
    private GUIStyle sTitulo, sSecao, sLabel, sBotaoGrande, sBotaoVerde,
                     sBotaoVermelho, sBotaoAzul, sItemVazio, sItemMissing;

    // Cores
    private static readonly Color cAzul    = new Color(0.20f, 0.55f, 1.00f);
    private static readonly Color cVerde   = new Color(0.15f, 0.75f, 0.40f);
    private static readonly Color cLaranja = new Color(1.00f, 0.55f, 0.10f);
    private static readonly Color cRoxo   = new Color(0.65f, 0.25f, 1.00f);
    private static readonly Color cVermel  = new Color(0.90f, 0.25f, 0.20f);

    // ── Estruturas de dados ──────────────────────────────────
    private class ResultadoItem
    {
        public GameObject go;
        public bool marcado = true;
        public string caminho;
        public int missingCount; // só para missing scripts
    }

    private class RelatorioData
    {
        public int totalObjetos, raiz, inativos;
        public int comSpriteRenderer, comCamera, comCollider;
        public int vazios, missing, semTag, tagUntagged;
        public Dictionary<string, int> porTag   = new Dictionary<string, int>();
        public Dictionary<string, int> porLayer = new Dictionary<string, int>();
    }

    // ════════════════════════════════════════════════════════
    //  ABRIR
    // ════════════════════════════════════════════════════════
    [MenuItem("Ferramentas/🗂 Gerenciador de Etiquetas & Faxina  %#f")]
    public static void Abrir()
    {
        var w = GetWindow<SceneCleaner>("🗂 Scene Cleaner");
        w.minSize = new Vector2(340, 560);
        w.Show();
    }

    // ════════════════════════════════════════════════════════
    //  OnGUI
    // ════════════════════════════════════════════════════════
    void OnGUI()
    {
        CarregarEstilos();
        DesenharCabecalho();
        abaAtiva = GUILayout.Toolbar(abaAtiva, abas, GUILayout.Height(30));
        EditorGUILayout.Space(8);

        switch (abaAtiva)
        {
            case 0: AbaTaguear();   break;
            case 1: AbaFaxina();    break;
            case 2: AbaRelatorio(); break;
        }
    }

    // ════════════════════════════════════════════════════════
    //  ABA 0 — TAGUEAMENTO EM MASSA
    // ════════════════════════════════════════════════════════
    void AbaTaguear()
    {
        int qtd = Selection.gameObjects?.Length ?? 0;

        // ── Info da Seleção ───────────────────────────────
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("SELEÇÃO ATUAL", sTitulo);
        EditorGUILayout.Space(4);

        MessageType tipo = qtd > 0 ? MessageType.Info : MessageType.Warning;
        EditorGUILayout.HelpBox(
            qtd == 0 ? "⚠ Selecione objetos na Hierarchy para taguear."
                     : $"✦ {qtd} objeto(s) selecionado(s)" +
                       (aplicarFilhos ? $" + filhos incluídos" : ""),
            tipo);

        aplicarFilhos = EditorGUILayout.Toggle("  Incluir filhos recursivamente:", aplicarFilhos);
        GUILayout.EndVertical();
        EditorGUILayout.Space(8);

        // ── Aplicar Tag ───────────────────────────────────
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("🏷  APLICAR TAG", sTitulo);
        EditorGUILayout.Space(4);

        string[] tags = InternalEditorUtility.tags;

        // Toggle para criar nova tag
        novaTagModo = EditorGUILayout.Toggle("  Criar nova tag:", novaTagModo);
        if (novaTagModo)
        {
            GUILayout.BeginHorizontal();
            novaTagNome = EditorGUILayout.TextField("  Nome:", novaTagNome);
            GUI.enabled = !string.IsNullOrEmpty(novaTagNome);
            if (GUILayout.Button("+ Criar", GUILayout.Width(60), GUILayout.Height(18)))
            {
                CriarTag(novaTagNome);
                novaTagNome   = "";
                novaTagModo   = false;
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
        else
        {
            tagIndex = EditorGUILayout.Popup("  Tag:", tagIndex, tags);
        }

        EditorGUILayout.Space(4);
        GUI.enabled = qtd > 0 && !novaTagModo;
        GUI.backgroundColor = cVerde;
        if (GUILayout.Button($"✅  Aplicar Tag  \"{(novaTagModo ? "..." : tags[tagIndex])}\"  em {qtd} objeto(s)", GUILayout.Height(34)))
            AplicarTag(tags[tagIndex]);
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;
        GUILayout.EndVertical();
        EditorGUILayout.Space(8);

        // ── Aplicar Layer ─────────────────────────────────
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("📦  APLICAR LAYER", sTitulo);
        EditorGUILayout.Space(4);

        string[] layers = InternalEditorUtility.layers;
        layerIndex = EditorGUILayout.Popup("  Layer:", layerIndex, layers);

        EditorGUILayout.Space(4);
        GUI.enabled = qtd > 0;
        GUI.backgroundColor = cAzul;
        if (GUILayout.Button($"✅  Aplicar Layer  \"{layers[layerIndex]}\"  em {qtd} objeto(s)", GUILayout.Height(34)))
            AplicarLayer(layers[layerIndex]);
        GUI.backgroundColor = Color.white;
        GUI.enabled = true;
        GUILayout.EndVertical();
        EditorGUILayout.Space(8);

        // ── Ações rápidas de tag ──────────────────────────
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("⚡  ATALHOS RÁPIDOS", sTitulo);
        EditorGUILayout.Space(4);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("🔵 UI",          GUILayout.Height(26))) AplicarTagRapida("UI");
        if (GUILayout.Button("🔴 Player",      GUILayout.Height(26))) AplicarTagRapida("Player");
        if (GUILayout.Button("🟡 Enemy",       GUILayout.Height(26))) AplicarTagRapida("Enemy");
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("🟢 Untagged",    GUILayout.Height(26))) AplicarTagRapida("Untagged");
        if (GUILayout.Button("⚪ MainCamera",  GUILayout.Height(26))) AplicarTagRapida("MainCamera");
        if (GUILayout.Button("🟤 GameCtrl",   GUILayout.Height(26))) AplicarTagRapida("GameController");
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  ABA 1 — FAXINA DE CENA
    // ════════════════════════════════════════════════════════
    void AbaFaxina()
    {
        // ── Controles de Varredura ────────────────────────
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("VARREDURA DA CENA", sTitulo);
        EditorGUILayout.Space(4);

        GUILayout.BeginHorizontal();
        mostrarVazios  = GUILayout.Toggle(mostrarVazios,  "👻 Objetos Vazios",    GUILayout.Width(140));
        mostrarMissing = GUILayout.Toggle(mostrarMissing, "💀 Scripts Quebrados", GUILayout.Width(140));
        GUILayout.EndHorizontal();

        EditorGUILayout.Space(6);
        GUI.backgroundColor = cAzul;
        if (GUILayout.Button("🔍  VARRER A CENA AGORA", sBotaoGrande))
            VarrerCena();
        GUI.backgroundColor = Color.white;
        GUILayout.EndVertical();
        EditorGUILayout.Space(8);

        if (!escaneou)
        {
            DesenharDicaFaxina();
            return;
        }

        scrollFaxina = GUILayout.BeginScrollView(scrollFaxina);

        // ── Bloco: Objetos Vazios ─────────────────────────
        if (mostrarVazios)
        {
            GUILayout.BeginVertical(sSecao);
            DesenharSubtitulo($"👻  OBJETOS VAZIOS ({resultadosVazios.Count})", cLaranja);
            EditorGUILayout.Space(4);

            if (resultadosVazios.Count == 0)
            {
                GUILayout.Label("  ✅ Nenhum objeto vazio encontrado.", sLabel);
            }
            else
            {
                BotoesSelecaoRapida(resultadosVazios);
                EditorGUILayout.Space(4);
                foreach (var item in resultadosVazios)
                    DesenharItemLista(item, cLaranja, "👻");

                EditorGUILayout.Space(6);
                int marcadosV = resultadosVazios.Count(r => r.marcado);
                GUI.enabled = marcadosV > 0;
                GUI.backgroundColor = cVermel;
                if (GUILayout.Button($"🗑  Deletar {marcadosV} Objeto(s) Vazio(s)", GUILayout.Height(32)))
                    DeletarMarcados(resultadosVazios, "Deletar Objetos Vazios");
                GUI.backgroundColor = Color.white;
                GUI.enabled = true;
            }
            GUILayout.EndVertical();
            EditorGUILayout.Space(8);
        }

        // ── Bloco: Scripts Quebrados ──────────────────────
        if (mostrarMissing)
        {
            GUILayout.BeginVertical(sSecao);
            DesenharSubtitulo($"💀  SCRIPTS QUEBRADOS / MISSING ({resultadosMissing.Count})", cVermel);
            EditorGUILayout.Space(4);

            if (resultadosMissing.Count == 0)
            {
                GUILayout.Label("  ✅ Nenhum script quebrado encontrado.", sLabel);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Scripts quebrados (Missing Scripts) aparecem quando um\n" +
                    "arquivo .cs é deletado ou renomeado sem atualizar os\n" +
                    "objetos que usavam esse script.",
                    MessageType.Warning);
                EditorGUILayout.Space(4);

                BotoesSelecaoRapida(resultadosMissing);
                EditorGUILayout.Space(4);

                foreach (var item in resultadosMissing)
                    DesenharItemLista(item, cVermel, $"💀 ×{item.missingCount}");

                EditorGUILayout.Space(6);
                int marcadosM = resultadosMissing.Count(r => r.marcado);
                GUI.enabled = marcadosM > 0;
                GUI.backgroundColor = cVermel;
                if (GUILayout.Button($"🧹  Limpar Scripts Quebrados em {marcadosM} Objeto(s)", GUILayout.Height(32)))
                    LimparMissingScripts(resultadosMissing.Where(r => r.marcado).Select(r => r.go).ToList());
                GUI.backgroundColor = Color.white;
                GUI.enabled = true;
            }
            GUILayout.EndVertical();
            EditorGUILayout.Space(8);
        }

        // ── Botão Faxina Completa ──────────────────────────
        if (resultadosVazios.Count > 0 || resultadosMissing.Count > 0)
        {
            GUILayout.BeginVertical(sSecao);
            GUI.backgroundColor = new Color(0.55f, 0.15f, 0.15f);
            if (GUILayout.Button("💥  FAXINA COMPLETA — Deletar TUDO marcado", sBotaoGrande))
                FaxinaCompleta();
            GUI.backgroundColor = Color.white;
            GUILayout.EndVertical();
        }

        GUILayout.EndScrollView();
    }

    // ════════════════════════════════════════════════════════
    //  ABA 2 — RELATÓRIO
    // ════════════════════════════════════════════════════════
    void AbaRelatorio()
    {
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("RELATÓRIO DA CENA", sTitulo);
        EditorGUILayout.Space(4);

        GUI.backgroundColor = cRoxo;
        if (GUILayout.Button("📊  Gerar Relatório Agora", GUILayout.Height(34)))
            GerarRelatorio();
        GUI.backgroundColor = Color.white;
        GUILayout.EndVertical();
        EditorGUILayout.Space(8);

        if (relatorio == null)
        {
            EditorGUILayout.HelpBox("Clique em 'Gerar Relatório' para analisar a cena.", MessageType.Info);
            return;
        }

        scrollRelatorio = GUILayout.BeginScrollView(scrollRelatorio);

        // ── Resumo Geral ──────────────────────────────────
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("RESUMO GERAL", sTitulo);
        EditorGUILayout.Space(4);

        LinhaRelatorio("Total de GameObjects",    relatorio.totalObjetos.ToString(), cAzul);
        LinhaRelatorio("Na raiz (sem pai)",       relatorio.raiz.ToString(),         cAzul);
        LinhaRelatorio("Inativos",                relatorio.inativos.ToString(),      cCinza());
        LinhaRelatorio("Com SpriteRenderer",      relatorio.comSpriteRenderer.ToString(), cVerde);
        LinhaRelatorio("Com Camera",              relatorio.comCamera.ToString(),     cVerde);
        LinhaRelatorio("Com Collider",            relatorio.comCollider.ToString(),   cVerde);
        GUILayout.EndVertical();
        EditorGUILayout.Space(6);

        // ── Problemas Detectados ──────────────────────────
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("⚠ PROBLEMAS DETECTADOS", sTitulo);
        EditorGUILayout.Space(4);

        LinhaRelatorio("👻 Objetos Vazios",        relatorio.vazios.ToString(),       relatorio.vazios   > 0 ? cLaranja : cVerde);
        LinhaRelatorio("💀 Scripts Quebrados",     relatorio.missing.ToString(),      relatorio.missing  > 0 ? cVermel  : cVerde);
        LinhaRelatorio("🏷 Sem Tag (Untagged)",    relatorio.tagUntagged.ToString(),  relatorio.tagUntagged > 0 ? cLaranja : cVerde);
        GUILayout.EndVertical();
        EditorGUILayout.Space(6);

        // ── Tags mais usadas ──────────────────────────────
        if (relatorio.porTag.Count > 0)
        {
            GUILayout.BeginVertical(sSecao);
            GUILayout.Label("DISTRIBUIÇÃO POR TAG", sTitulo);
            EditorGUILayout.Space(4);
            foreach (var par in relatorio.porTag.OrderByDescending(p => p.Value))
            {
                float pct = (float)par.Value / relatorio.totalObjetos;
                GUILayout.BeginHorizontal();
                GUILayout.Label($"  {par.Key}", sLabel, GUILayout.Width(130));
                Rect barraRect = GUILayoutUtility.GetRect(0, 14, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(barraRect, new Color(0.15f, 0.15f, 0.22f));
                EditorGUI.DrawRect(new Rect(barraRect.x, barraRect.y, barraRect.width * pct, barraRect.height), cAzul);
                GUILayout.Label($"{par.Value}", sLabel, GUILayout.Width(30));
                GUILayout.EndHorizontal();
                EditorGUILayout.Space(2);
            }
            GUILayout.EndVertical();
            EditorGUILayout.Space(6);
        }

        // ── Layers mais usadas ────────────────────────────
        if (relatorio.porLayer.Count > 0)
        {
            GUILayout.BeginVertical(sSecao);
            GUILayout.Label("DISTRIBUIÇÃO POR LAYER", sTitulo);
            EditorGUILayout.Space(4);
            foreach (var par in relatorio.porLayer.OrderByDescending(p => p.Value))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"  {par.Key}", sLabel, GUILayout.Width(130));
                Rect barraRect = GUILayoutUtility.GetRect(0, 14, GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(barraRect, new Color(0.15f, 0.15f, 0.22f));
                float pct = (float)par.Value / relatorio.totalObjetos;
                EditorGUI.DrawRect(new Rect(barraRect.x, barraRect.y, barraRect.width * pct, barraRect.height), cRoxo);
                GUILayout.Label($"{par.Value}", sLabel, GUILayout.Width(30));
                GUILayout.EndHorizontal();
                EditorGUILayout.Space(2);
            }
            GUILayout.EndVertical();
        }

        GUILayout.EndScrollView();
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — TAGUEAR
    // ════════════════════════════════════════════════════════
    void AplicarTag(string tag)
    {
        var alvos = ObterAlvos();
        Undo.RecordObjects(alvos.Cast<Object>().ToArray(), "Aplicar Tag");
        foreach (var go in alvos) go.tag = tag;

        Debug.LogWarning($"[🗂 SceneCleaner] Tag \"{tag}\" aplicada em {alvos.Count} objeto(s).");
    }

    void AplicarTagRapida(string tag)
    {
        // Verifica se a tag existe; se não, informa sem criar erro
        try { _ = GameObject.FindGameObjectsWithTag(tag); }
        catch
        {
            Debug.LogWarning($"[🗂 SceneCleaner] Tag \"{tag}\" não existe no projeto. Crie-a em Edit → Project Settings → Tags and Layers.");
            EditorUtility.DisplayDialog("Tag inexistente",
                $"A tag \"{tag}\" não existe neste projeto.\n\nCrie-a em:\nEdit → Project Settings → Tags and Layers", "OK");
            return;
        }
        AplicarTag(tag);
    }

    void AplicarLayer(string layerName)
    {
        int layerIdx = LayerMask.NameToLayer(layerName);
        if (layerIdx < 0)
        {
            Debug.LogWarning($"[🗂 SceneCleaner] Layer \"{layerName}\" não encontrada.");
            return;
        }

        var alvos = ObterAlvos();
        Undo.RecordObjects(alvos.Cast<Object>().ToArray(), "Aplicar Layer");
        foreach (var go in alvos) go.layer = layerIdx;

        Debug.LogWarning($"[🗂 SceneCleaner] Layer \"{layerName}\" aplicada em {alvos.Count} objeto(s).");
    }

    void CriarTag(string nome)
    {
        if (string.IsNullOrEmpty(nome)) return;

        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));
        SerializedProperty tagsProp = tagManager.FindProperty("tags");

        // Verifica se já existe
        for (int i = 0; i < tagsProp.arraySize; i++)
            if (tagsProp.GetArrayElementAtIndex(i).stringValue == nome)
            {
                Debug.LogWarning($"[🗂 SceneCleaner] Tag \"{nome}\" já existe.");
                return;
            }

        tagsProp.arraySize++;
        tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = nome;
        tagManager.ApplyModifiedProperties();

        Debug.LogWarning($"[🗂 SceneCleaner] Tag \"{nome}\" criada com sucesso.");
        EditorUtility.DisplayDialog("Tag Criada ✅", $"Tag \"{nome}\" criada no projeto!", "OK");
    }

    List<GameObject> ObterAlvos()
    {
        if (!aplicarFilhos) return Selection.gameObjects.ToList();

        HashSet<GameObject> resultado = new HashSet<GameObject>();
        foreach (var go in Selection.gameObjects)
        {
            resultado.Add(go);
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                resultado.Add(t.gameObject);
        }
        return resultado.ToList();
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — FAXINA
    // ════════════════════════════════════════════════════════
    void VarrerCena()
    {
        resultadosVazios.Clear();
        resultadosMissing.Clear();
        escaneou = true;

        GameObject[] todos = FindObjectsOfType<GameObject>(includeInactive: true);
        int totalVazios = 0, totalMissing = 0;

        foreach (var go in todos)
        {
            // ── Vazios ────────────────────────────────────
            if (mostrarVazios && EhVazio(go))
            {
                resultadosVazios.Add(new ResultadoItem
                {
                    go      = go,
                    marcado = true,
                    caminho = Caminho(go)
                });
                totalVazios++;
            }

            // ── Missing Scripts ───────────────────────────
            if (mostrarMissing)
            {
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                if (missing > 0)
                {
                    resultadosMissing.Add(new ResultadoItem
                    {
                        go           = go,
                        marcado      = true,
                        caminho      = Caminho(go),
                        missingCount = missing
                    });
                    totalMissing++;
                }
            }
        }

        // ⚠ Sempre LogWarning — nunca LogError
        Debug.LogWarning(
            $"[🗂 SceneCleaner] Varredura concluída → " +
            $"👻 {totalVazios} vazio(s)  |  💀 {totalMissing} missing script(s).");

        Repaint();
    }

    bool EhVazio(GameObject go)
    {
        if (go.transform.childCount > 0) return false;
        return go.GetComponents<Component>().All(c => c is Transform || c == null);
    }

    void DeletarMarcados(List<ResultadoItem> lista, string nomeUndo)
    {
        var alvos = lista.Where(r => r.marcado && r.go != null).ToList();
        if (alvos.Count == 0) return;

        bool ok = EditorUtility.DisplayDialog(
            $"⚠ Confirmar Exclusão",
            $"Deletar {alvos.Count} objeto(s)?\n\nUso do Ctrl+Z para desfazer estará disponível.",
            "Sim, deletar", "Cancelar");
        if (!ok) return;

        Undo.SetCurrentGroupName(nomeUndo);
        int grupo = Undo.GetCurrentGroup();

        int count = 0;
        foreach (var item in alvos)
        {
            if (item.go == null) continue;
            Undo.DestroyObjectImmediate(item.go);
            count++;
        }

        Undo.CollapseUndoOperations(grupo);
        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        // ⚠ LogWarning apenas
        Debug.LogWarning($"[🗂 SceneCleaner] {count} objeto(s) deletado(s). Use Ctrl+Z para desfazer.");

        VarrerCena();
    }

    void LimparMissingScripts(List<GameObject> alvos)
    {
        if (alvos.Count == 0) return;

        bool ok = EditorUtility.DisplayDialog(
            "⚠ Remover Scripts Quebrados",
            $"Remover missing scripts de {alvos.Count} objeto(s)?\n\nEsta ação pode ser desfeita com Ctrl+Z.",
            "Sim, limpar", "Cancelar");
        if (!ok) return;

        Undo.SetCurrentGroupName("Remover Missing Scripts");
        int grupo = Undo.GetCurrentGroup();
        int total = 0;

        foreach (var go in alvos)
        {
            if (go == null) continue;
            Undo.RecordObject(go, "Remove Missing Script");
            int antes = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            total += antes;
        }

        Undo.CollapseUndoOperations(grupo);
        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        // ⚠ LogWarning apenas
        Debug.LogWarning(
            $"[🗂 SceneCleaner] 💀 {total} missing script(s) removido(s) de {alvos.Count} objeto(s).");

        VarrerCena();
        EditorUtility.DisplayDialog("Limpeza Concluída ✅",
            $"{total} script(s) quebrado(s) removido(s).\nUse Ctrl+Z para desfazer.", "OK");
    }

    void FaxinaCompleta()
    {
        int totalVazios  = resultadosVazios.Count(r => r.marcado);
        int totalMissing = resultadosMissing.Count(r => r.marcado);

        bool ok = EditorUtility.DisplayDialog(
            "💥 FAXINA COMPLETA",
            $"Isso irá:\n\n" +
            $"🗑 Deletar {totalVazios} objeto(s) vazio(s)\n" +
            $"🧹 Limpar {totalMissing} objeto(s) com scripts quebrados\n\n" +
            "Todos os passos são desfeitos com Ctrl+Z.",
            "Sim, fazer faxina total", "Cancelar");

        if (!ok) return;

        // Deleta vazios
        Undo.SetCurrentGroupName("Faxina Completa");
        int grupo = Undo.GetCurrentGroup();

        foreach (var item in resultadosVazios.Where(r => r.marcado && r.go != null))
            Undo.DestroyObjectImmediate(item.go);

        // Limpa missing
        foreach (var item in resultadosMissing.Where(r => r.marcado && r.go != null))
        {
            Undo.RecordObject(item.go, "Remove Missing");
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(item.go);
        }

        Undo.CollapseUndoOperations(grupo);
        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        // ⚠ LogWarning apenas
        Debug.LogWarning(
            $"[🗂 SceneCleaner] 💥 Faxina completa: " +
            $"{totalVazios} objeto(s) deletado(s), {totalMissing} missing script(s) removido(s).");

        VarrerCena();
        EditorUtility.DisplayDialog("Faxina Concluída! ✅",
            $"✔ {totalVazios} vazio(s) deletado(s)\n✔ {totalMissing} missing(s) limpos\n\nUse Ctrl+Z para desfazer.", "OK");
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — RELATÓRIO
    // ════════════════════════════════════════════════════════
    void GerarRelatorio()
    {
        relatorio = new RelatorioData();
        GameObject[] todos = FindObjectsOfType<GameObject>(includeInactive: true);

        relatorio.totalObjetos = todos.Length;

        foreach (var go in todos)
        {
            if (go.transform.parent == null) relatorio.raiz++;
            if (!go.activeSelf)             relatorio.inativos++;

            if (go.GetComponent<SpriteRenderer>()) relatorio.comSpriteRenderer++;
            if (go.GetComponent<Camera>())          relatorio.comCamera++;
            if (go.GetComponent<Collider2D>() || go.GetComponent<Collider>()) relatorio.comCollider++;

            if (EhVazio(go)) relatorio.vazios++;
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go) > 0) relatorio.missing++;

            // Tags
            string tag = go.tag;
            if (tag == "Untagged") relatorio.tagUntagged++;
            if (!relatorio.porTag.ContainsKey(tag)) relatorio.porTag[tag] = 0;
            relatorio.porTag[tag]++;

            // Layers
            string layer = LayerMask.LayerToName(go.layer);
            if (!relatorio.porLayer.ContainsKey(layer)) relatorio.porLayer[layer] = 0;
            relatorio.porLayer[layer]++;
        }

        Debug.LogWarning(
            $"[🗂 SceneCleaner] Relatório: {relatorio.totalObjetos} objetos | " +
            $"{relatorio.vazios} vazios | {relatorio.missing} missing | {relatorio.tagUntagged} untagged.");
        Repaint();
    }

    // ════════════════════════════════════════════════════════
    //  UI — COMPONENTES
    // ════════════════════════════════════════════════════════
    void DesenharItemLista(ResultadoItem item, Color cor, string badge)
    {
        if (item.go == null) return;

        Rect r = GUILayoutUtility.GetRect(0, 22, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(r, item.marcado ? new Color(0.20f, 0.10f, 0.10f) : new Color(0.13f, 0.13f, 0.20f));
        EditorGUI.DrawRect(new Rect(r.x, r.y, 3, r.height), cor);

        Rect checkR = new Rect(r.x + 7, r.y + 3, 16, 16);
        item.marcado = EditorGUI.Toggle(checkR, item.marcado);

        Rect badgeR = new Rect(r.x + 28, r.y + 3, 40, 16);
        EditorGUI.LabelField(badgeR, badge, EditorStyles.miniLabel);

        Rect nameR = new Rect(r.x + 70, r.y + 3, r.width - 118, 16);
        EditorGUI.LabelField(nameR, item.caminho, sLabel);

        Rect pingR = new Rect(r.xMax - 44, r.y + 2, 42, 18);
        if (GUI.Button(pingR, "ping", EditorStyles.miniButton))
        {
            Selection.activeGameObject = item.go;
            EditorGUIUtility.PingObject(item.go);
        }

        EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), new Color(0.22f, 0.22f, 0.30f));
    }

    void BotoesSelecaoRapida(List<ResultadoItem> lista)
    {
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("✅ Todos",    GUILayout.Height(20))) lista.ForEach(r => r.marcado = true);
        if (GUILayout.Button("☐ Nenhum",   GUILayout.Height(20))) lista.ForEach(r => r.marcado = false);
        if (GUILayout.Button("👁 Selec.",  GUILayout.Height(20)))
            Selection.objects = lista.Where(r => r.go != null).Select(r => (Object)r.go).ToArray();
        GUILayout.EndHorizontal();
    }

    void LinhaRelatorio(string label, string valor, Color cor)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label($"  {label}", sLabel, GUILayout.Width(190));
        var st = new GUIStyle(EditorStyles.boldLabel)
        { normal = { textColor = cor }, fontSize = 11 };
        GUILayout.Label(valor, st);
        GUILayout.EndHorizontal();
        EditorGUILayout.Space(1);
    }

    void DesenharDicaFaxina()
    {
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("O QUE A FAXINA REMOVE?", sTitulo);
        EditorGUILayout.Space(4);

        var itens = new[]
        {
            "👻 GameObjects com APENAS Transform (sem filhos)",
            "💀 Componentes Missing Script (null/quebrados)",
            "    — ocorrem quando scripts são deletados/renomeados",
            "    — causam erros na Build e bugs de física",
        };
        foreach (var i in itens) GUILayout.Label($"  {i}", sLabel);

        EditorGUILayout.Space(8);

        var stNota = new GUIStyle(EditorStyles.miniLabel)
        {
            wordWrap = true,
            normal   = { textColor = new Color(0.6f, 0.6f, 0.7f) }
        };
        GUILayout.Label(
            "💡 Todos os avisos usam LogWarning (🟡 amarelo).\n" +
            "Nenhum LogError vermelho é gerado — o git e CI/CD\n" +
            "não são afetados por esta ferramenta.",
            stNota);
        GUILayout.EndVertical();
    }

    void DesenharSubtitulo(string texto, Color cor)
    {
        var st = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = cor }, fontSize = 10 };
        GUILayout.Label(texto, st);
        Rect r = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(r, new Color(cor.r, cor.g, cor.b, 0.3f));
    }

    string Caminho(GameObject go)
    {
        string p = go.name;
        Transform pai = go.transform.parent;
        int depth = 0;
        while (pai != null && depth++ < 3) { p = pai.name + "/" + p; pai = pai.parent; }
        return pai != null ? ".../" + p : p;
    }

    Color cCinza() => new Color(0.55f, 0.55f, 0.65f);

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
        sSecao       = new GUIStyle(EditorStyles.helpBox) { padding = new RectOffset(10, 10, 8, 8) };
        sLabel       = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.75f, 0.75f, 0.85f) } };
        sBotaoGrande = new GUIStyle(GUI.skin.button) { fontSize = 11, fontStyle = FontStyle.Bold, fixedHeight = 36 };
        sBotaoVerde  = new GUIStyle(GUI.skin.button) { fontSize = 10, fixedHeight = 30 };
        sBotaoVermelho = new GUIStyle(GUI.skin.button) { fontSize = 10, fixedHeight = 30 };
        sBotaoAzul   = new GUIStyle(GUI.skin.button) { fontSize = 10, fixedHeight = 30 };

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
        GUILayout.Label("🗂  ETIQUETAS & FAXINA DE CENA", st);
        Rect r = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(r, new Color(0.4f, 0.85f, 1f, 0.4f));
        EditorGUILayout.Space(6);
    }

    void OnSelectionChange() => Repaint();
}
