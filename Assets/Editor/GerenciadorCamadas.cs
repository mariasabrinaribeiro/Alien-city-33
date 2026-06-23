using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

// ============================================================
// GerenciadorCamadas.cs  —  Ferramenta de Editor
//
// Abre em: Ferramentas → 🎭 Gerenciador de Camadas (Frente/Trás)
//          Atalho: Ctrl + Shift + L
//
// Funções:
//   🔼 Trazer objeto para frente (aumenta Order in Layer)
//   🔽 Mandar objeto para trás  (diminui Order in Layer)
//   👁  Diagnóstico de invisibilidade automático
//   🎨 Visualizar e editar Sorting Layers de todos os objetos
// ============================================================

public class GerenciadorCamadas : EditorWindow
{
    // ── Estado ───────────────────────────────────────────────
    private Vector2 scroll;
    private Vector2 scrollDiag;
    private List<RendererInfo> todosRenderers = new List<RendererInfo>();
    private List<string> problemasDetectados  = new List<string>();
    private int abaAtiva = 0;
    private static readonly string[] abas = { "🎭 Camadas", "👁 Diagnóstico" };

    private struct RendererInfo
    {
        public GameObject go;
        public Renderer   rend;
        public string     sortingLayer;
        public int        orderInLayer;
        public bool       visivel;
        public string     tipoRenderer;
    }

    [MenuItem("Ferramentas/🎭 Gerenciador de Camadas (Frente-Trás)  %#l")]
    public static void Abrir()
    {
        var w = GetWindow<GerenciadorCamadas>("🎭 Camadas");
        w.minSize = new Vector2(480, 560);
        w.Scan();
    }

    // ── GUI Principal ────────────────────────────────────────
    void OnGUI()
    {
        DrawCabecalho();

        abaAtiva = GUILayout.Toolbar(abaAtiva, abas, GUILayout.Height(30));
        EditorGUILayout.Space(6);

        if (abaAtiva == 0) DrawAbaCamadas();
        else               DrawAbaDiagnostico();
    }

    // ── Cabeçalho ────────────────────────────────────────────
    void DrawCabecalho()
    {
        GUIStyle tit = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 16,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(0.6f, 0.9f, 1f) }
        };
        GUIStyle sub = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(0.7f, 0.7f, 0.8f) }
        };

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("🎭 Gerenciador de Camadas", tit);
        EditorGUILayout.LabelField("Controle quem fica na frente e atrás — e detecta objetos invisíveis", sub);
        EditorGUILayout.Space(6);

        if (GUILayout.Button("🔄 Atualizar lista", GUILayout.Height(26))) Scan();
        EditorGUILayout.Space(4);
        Divisor();
    }

    // ═══════════════════════════════════════════════════════
    //  ABA 1 — CAMADAS
    // ═══════════════════════════════════════════════════════
    void DrawAbaCamadas()
    {
        // Botões rápidos para seleção atual
        DrawBotoesRapidos();
        Divisor();

        // Lista de todos os renderers
        EditorGUILayout.LabelField($"📋 Todos os objetos renderizáveis ({todosRenderers.Count})", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        // Cabeçalho da tabela
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        EditorGUILayout.LabelField("Objeto",          GUILayout.Width(160));
        EditorGUILayout.LabelField("Tipo",            GUILayout.Width(75));
        EditorGUILayout.LabelField("Sorting Layer",   GUILayout.Width(90));
        EditorGUILayout.LabelField("Order",           GUILayout.Width(45));
        EditorGUILayout.LabelField("👁",              GUILayout.Width(20));
        EditorGUILayout.LabelField("Ações",           GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();

        scroll = EditorGUILayout.BeginScrollView(scroll);

        for (int i = 0; i < todosRenderers.Count; i++)
        {
            var info = todosRenderers[i];
            if (info.go == null || info.rend == null) continue;

            bool selecionado = Selection.activeGameObject == info.go;
            Color corFundo   = selecionado
                ? new Color(0.3f, 0.5f, 0.8f, 0.4f)
                : (i % 2 == 0 ? new Color(0.2f, 0.2f, 0.25f) : new Color(0.18f, 0.18f, 0.22f));

            Rect linha = EditorGUILayout.BeginHorizontal(GUILayout.Height(24));
            EditorGUI.DrawRect(linha, corFundo);

            // Nome
            if (GUILayout.Button(info.go.name, EditorStyles.linkLabel, GUILayout.Width(160)))
            {
                Selection.activeGameObject = info.go;
                SceneView.lastActiveSceneView?.FrameSelected();
            }

            // Tipo
            EditorGUILayout.LabelField(info.tipoRenderer, EditorStyles.miniLabel, GUILayout.Width(75));

            // Sorting Layer
            string novoLayer = info.rend.sortingLayerName;
            string[] layers  = GetSortingLayerNames();
            int idx          = Mathf.Max(0, System.Array.IndexOf(layers, novoLayer));
            int novoIdx      = EditorGUILayout.Popup(idx, layers, GUILayout.Width(90));
            if (novoIdx != idx)
            {
                Undo.RecordObject(info.rend, "Mudar Sorting Layer");
                info.rend.sortingLayerName = layers[novoIdx];
                todosRenderers[i] = BuildInfo(info.go, info.rend);
            }

            // Order in Layer
            int novoOrder = EditorGUILayout.IntField(info.rend.sortingOrder, GUILayout.Width(45));
            if (novoOrder != info.rend.sortingOrder)
            {
                Undo.RecordObject(info.rend, "Mudar Order in Layer");
                info.rend.sortingOrder = novoOrder;
                todosRenderers[i] = BuildInfo(info.go, info.rend);
            }

            // Visível?
            string iconeVis = info.visivel ? "✅" : "⚠️";
            EditorGUILayout.LabelField(iconeVis, GUILayout.Width(20));

            // Botões frente/trás
            if (GUILayout.Button("🔼", GUILayout.Width(28), GUILayout.Height(20)))
            {
                Undo.RecordObject(info.rend, "Frente");
                info.rend.sortingOrder++;
                todosRenderers[i] = BuildInfo(info.go, info.rend);
            }
            if (GUILayout.Button("🔽", GUILayout.Width(28), GUILayout.Height(20)))
            {
                Undo.RecordObject(info.rend, "Trás");
                info.rend.sortingOrder--;
                todosRenderers[i] = BuildInfo(info.go, info.rend);
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    void DrawBotoesRapidos()
    {
        GameObject sel = Selection.activeGameObject;

        EditorGUILayout.LabelField("⚡ Ações Rápidas — Objeto Selecionado", EditorStyles.boldLabel);

        if (sel == null)
        {
            EditorGUILayout.HelpBox("Selecione um objeto na Hierarchy ou Scene para usar as ações rápidas.", MessageType.Info);
            return;
        }

        Renderer rend = sel.GetComponent<Renderer>();
        if (rend == null)
        {
            EditorGUILayout.HelpBox($"'{sel.name}' não tem Renderer. Selecione um Sprite ou Mesh.", MessageType.Warning);
            return;
        }

        EditorGUILayout.HelpBox(
            $"Selecionado: {sel.name}\n" +
            $"Sorting Layer: {rend.sortingLayerName}   |   Order in Layer: {rend.sortingOrder}",
            MessageType.None);

        EditorGUILayout.BeginHorizontal();

        GUIStyle bVerde = new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold, fixedHeight = 36 };
        GUIStyle bAzul  = new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold, fixedHeight = 36 };

        if (GUILayout.Button("🔼  FRENTE\n(+1 Order)", bVerde))
        {
            Undo.RecordObject(rend, "Trazer para Frente");
            rend.sortingOrder++;
            Debug.Log($"[Camadas] {sel.name} → Order in Layer = {rend.sortingOrder}");
            Scan();
        }

        if (GUILayout.Button("🔽  TRÁS\n(-1 Order)", bAzul))
        {
            Undo.RecordObject(rend, "Mandar para Trás");
            rend.sortingOrder--;
            Debug.Log($"[Camadas] {sel.name} → Order in Layer = {rend.sortingOrder}");
            Scan();
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("⏫ Frente de TUDO", GUILayout.Height(28)))
        {
            int maxOrder = todosRenderers.Max(r => r.orderInLayer);
            Undo.RecordObject(rend, "Frente de Tudo");
            rend.sortingOrder = maxOrder + 1;
            Debug.Log($"[Camadas] {sel.name} → Order in Layer = {rend.sortingOrder} (frente de tudo)");
            Scan();
        }

        if (GUILayout.Button("⏬ Atrás de TUDO", GUILayout.Height(28)))
        {
            int minOrder = todosRenderers.Min(r => r.orderInLayer);
            Undo.RecordObject(rend, "Atrás de Tudo");
            rend.sortingOrder = minOrder - 1;
            Debug.Log($"[Camadas] {sel.name} → Order in Layer = {rend.sortingOrder} (atrás de tudo)");
            Scan();
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(6);
    }

    // ═══════════════════════════════════════════════════════
    //  ABA 2 — DIAGNÓSTICO DE INVISIBILIDADE
    // ═══════════════════════════════════════════════════════
    void DrawAbaDiagnostico()
    {
        EditorGUILayout.LabelField("👁 Diagnóstico de Invisibilidade", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        if (GUILayout.Button("🔍 Rodar Diagnóstico Completo", GUILayout.Height(36)))
            RodarDiagnostico();

        EditorGUILayout.Space(6);

        if (problemasDetectados.Count == 0)
        {
            EditorGUILayout.HelpBox("Clique em 'Rodar Diagnóstico' para verificar a cena.", MessageType.Info);
            return;
        }

        // Resumo
        int erros   = problemasDetectados.Count(p => p.StartsWith("❌"));
        int avisos  = problemasDetectados.Count(p => p.StartsWith("⚠️"));
        int ok      = problemasDetectados.Count(p => p.StartsWith("✅"));

        EditorGUILayout.BeginHorizontal();
        GUIStyle styleErro  = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(1f, 0.4f, 0.4f) } };
        GUIStyle styleAviso = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(1f, 0.85f, 0.2f) } };
        GUIStyle styleOk    = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = new Color(0.4f, 1f, 0.5f) } };
        EditorGUILayout.LabelField($"❌ {erros} erros", styleErro,  GUILayout.Width(100));
        EditorGUILayout.LabelField($"⚠️ {avisos} avisos", styleAviso, GUILayout.Width(100));
        EditorGUILayout.LabelField($"✅ {ok} OK", styleOk, GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();

        Divisor();

        scrollDiag = EditorGUILayout.BeginScrollView(scrollDiag);
        foreach (var msg in problemasDetectados)
        {
            MessageType tipo = msg.StartsWith("❌") ? MessageType.Error
                             : msg.StartsWith("⚠️") ? MessageType.Warning
                             : MessageType.Info;
            EditorGUILayout.HelpBox(msg, tipo);
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(6);
        EditorGUILayout.HelpBox(
            "💡 CAUSA MAIS COMUM de sumir ao entrar na cena:\n" +
            "O fundo (background/céu) tem Order in Layer MAIOR que o personagem.\n" +
            "Solução: Selecione o fundo → use 🔽 TRÁS, ou selecione o personagem → use 🔼 FRENTE.",
            MessageType.Warning);
    }

    // ── Diagnóstico ──────────────────────────────────────────
    void RodarDiagnostico()
    {
        problemasDetectados.Clear();
        var todos = GameObject.FindObjectsOfType<GameObject>();

        foreach (var go in todos)
        {
            if (go == null) continue;

            // 1. Escala zero
            Vector3 s = go.transform.localScale;
            if (s.x == 0 || s.y == 0 || s.z == 0)
                problemasDetectados.Add($"❌ '{go.name}' — Escala tem eixo ZERO ({s.x:F2}, {s.y:F2}, {s.z:F2}). Objeto invisível!");

            // 2. Renderer desligado
            Renderer rend = go.GetComponent<Renderer>();
            if (rend != null && !rend.enabled && go.activeInHierarchy)
                problemasDetectados.Add($"⚠️ '{go.name}' — Renderer DESATIVADO mas GameObject ativo.");

            // 3. SpriteRenderer com alpha 0
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            if (sr != null && sr.color.a < 0.01f)
                problemasDetectados.Add($"❌ '{go.name}' — SpriteRenderer com alpha = 0. Completamente transparente!");

            // 4. CanvasGroup alpha 0
            CanvasGroup cg = go.GetComponent<CanvasGroup>();
            if (cg != null && cg.alpha < 0.01f)
                problemasDetectados.Add($"⚠️ '{go.name}' — CanvasGroup com alpha = 0.");

            // 5. Posição Z muito fundo
            float z = go.transform.position.z;
            if (Mathf.Abs(z) > 200f)
                problemasDetectados.Add($"⚠️ '{go.name}' — Posição Z absurda: {z:F1}. Pode estar fora da câmera.");

            // 6. SpriteRenderer sem sprite
            if (sr != null && sr.sprite == null)
                problemasDetectados.Add($"⚠️ '{go.name}' — SpriteRenderer sem sprite atribuído.");
        }

        // 7. Verificar ordem do fundo vs personagens
        var todos2D = todosRenderers.OrderBy(r => r.orderInLayer).ToList();
        if (todos2D.Count > 1)
        {
            var fundo = todos2D.FirstOrDefault(r =>
                r.go.name.ToLower().Contains("fundo") ||
                r.go.name.ToLower().Contains("ceu") ||
                r.go.name.ToLower().Contains("background") ||
                r.go.name.ToLower().Contains("sky") ||
                r.go.name.ToLower().Contains("predio"));

            var personagem = todos2D.LastOrDefault(r =>
                r.go.name.ToLower().Contains("player") ||
                r.go.name.ToLower().Contains("personag") ||
                r.go.name.ToLower().Contains("mae") ||
                r.go.name.ToLower().Contains("filho") ||
                r.go.name.ToLower().Contains("bicho"));

            if (fundo.go != null && personagem.go != null)
            {
                if (fundo.orderInLayer > personagem.orderInLayer)
                    problemasDetectados.Add(
                        $"❌ PROBLEMA DE CAMADA: '{fundo.go.name}' (Order={fundo.orderInLayer}) está NA FRENTE de " +
                        $"'{personagem.go.name}' (Order={personagem.orderInLayer}). " +
                        $"Isso causa invisibilidade! Use 🔽 TRÁS no fundo ou 🔼 FRENTE no personagem.");
                else
                    problemasDetectados.Add($"✅ Ordem de camadas entre fundo e personagem parece correta.");
            }
        }

        if (problemasDetectados.Count == 0)
            problemasDetectados.Add("✅ Nenhum problema de visibilidade detectado na cena!");

        Repaint();
    }

    // ── Scan da cena ─────────────────────────────────────────
    void Scan()
    {
        todosRenderers.Clear();
        var renderers = GameObject.FindObjectsOfType<Renderer>();
        foreach (var r in renderers)
        {
            if (r == null || r.gameObject == null) continue;
            todosRenderers.Add(BuildInfo(r.gameObject, r));
        }
        todosRenderers = todosRenderers.OrderBy(x => x.orderInLayer).ToList();
        Repaint();
    }

    RendererInfo BuildInfo(GameObject go, Renderer r)
    {
        bool visivel = go.activeInHierarchy && r.enabled;

        SpriteRenderer sr = r as SpriteRenderer;
        if (sr != null && sr.color.a < 0.01f) visivel = false;

        Vector3 s = go.transform.localScale;
        if (s.x == 0 || s.y == 0) visivel = false;

        return new RendererInfo
        {
            go           = go,
            rend         = r,
            sortingLayer  = r.sortingLayerName,
            orderInLayer  = r.sortingOrder,
            visivel       = visivel,
            tipoRenderer  = r.GetType().Name.Replace("Renderer", "")
        };
    }

    // ── Helpers ──────────────────────────────────────────────
    void Divisor()
    {
        Rect r = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(r, new Color(0.35f, 0.35f, 0.4f));
        EditorGUILayout.Space(4);
    }

    string[] GetSortingLayerNames()
    {
        var layers = new List<string>();
        foreach (var layer in SortingLayer.layers)
            layers.Add(layer.name);
        if (layers.Count == 0) layers.Add("Default");
        return layers.ToArray();
    }

    void OnSelectionChange() => Repaint();
}
