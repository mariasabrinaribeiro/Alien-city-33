using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.Linq;

// ============================================================
// Vassoureiro.cs  —  Ferramenta de Editor
//
// Abre em: Ferramentas → 🧹 Vassoureiro de GameObjects Vazios
//          Atalho: Ctrl + Shift + V
//
// O que faz:
//   • Varre a cena procurando GameObjects "fantasmas"
//     (apenas Transform, sem filhos, sem componentes úteis)
//   • Mostra uma lista de preview ANTES de deletar qualquer coisa
//   • Permite excluir objetos da lista manualmente
//   • Deleta tudo de uma vez com suporte a Ctrl+Z
//   • Usa SOMENTE Debug.LogWarning — nunca LogError
//     (não sobe erro no git, não polui o console com vermelho)
// ============================================================

public class Vassoureiro : EditorWindow
{
    // ── Estado ───────────────────────────────────────────────
    private List<GameObject>  encontrados   = new List<GameObject>();
    private List<bool>        marcados      = new List<bool>();
    private Vector2           scroll;
    private bool              escaneou      = false;
    private bool              estilosOk     = false;
    private string            filtroNome    = "";
    private bool              mostrarRaiz   = true;
    private bool              mostrarNested = true;

    // Estilos
    private GUIStyle sTitulo, sSecao, sLabel, sLabelPerigo, sBotaoVarrer,
                     sBotaoDeletar, sBotaoNeutro, sItem, sItemMarcado;

    // Cores
    private static readonly Color cAzul     = new Color(0.20f, 0.55f, 1.00f);
    private static readonly Color cVerde    = new Color(0.15f, 0.75f, 0.40f);
    private static readonly Color cLaranja  = new Color(1.00f, 0.55f, 0.10f);
    private static readonly Color cVermelho = new Color(0.90f, 0.25f, 0.20f);
    private static readonly Color cFundoItem= new Color(0.14f, 0.14f, 0.20f);
    private static readonly Color cFundoMarcado = new Color(0.30f, 0.10f, 0.10f);

    // ════════════════════════════════════════════════════════
    //  ABRIR
    // ════════════════════════════════════════════════════════
    [MenuItem("Ferramentas/🧹 Vassoureiro de GameObjects Vazios  %#v")]
    public static void Abrir()
    {
        var w = GetWindow<Vassoureiro>("🧹 Vassoureiro");
        w.minSize = new Vector2(340, 520);
        w.Show();
    }

    // ════════════════════════════════════════════════════════
    //  OnGUI
    // ════════════════════════════════════════════════════════
    void OnGUI()
    {
        CarregarEstilos();
        DesenharCabecalho();

        GUILayout.BeginVertical(sSecao);
        DesenharPainelVarredura();
        GUILayout.EndVertical();
        EditorGUILayout.Space(8);

        if (escaneou)
        {
            if (encontrados.Count == 0)
            {
                DesenharCenaLimpa();
            }
            else
            {
                DesenharResultados();
                EditorGUILayout.Space(8);
                DesenharPainelAcao();
            }
        }
        else
        {
            DesenharInstrucoes();
        }
    }

    // ════════════════════════════════════════════════════════
    //  CABEÇALHO
    // ════════════════════════════════════════════════════════
    void DesenharCabecalho()
    {
        EditorGUILayout.Space(6);
        var st = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 14,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(0.5f, 0.85f, 1f) }
        };
        GUILayout.Label("🧹  VASSOUREIRO DE OBJETOS VAZIOS", st);
        EditorGUILayout.Space(2);
        Rect r = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(r, new Color(0.4f, 0.85f, 1f, 0.4f));
        EditorGUILayout.Space(8);
    }

    // ════════════════════════════════════════════════════════
    //  PAINEL DE VARREDURA
    // ════════════════════════════════════════════════════════
    void DesenharPainelVarredura()
    {
        GUILayout.Label("VARREDURA DA CENA", sTitulo);
        EditorGUILayout.Space(4);

        // Filtros
        GUILayout.BeginHorizontal();
        mostrarRaiz   = GUILayout.Toggle(mostrarRaiz,   "📂 Na raiz",    GUILayout.Width(90));
        mostrarNested = GUILayout.Toggle(mostrarNested, "📁 Aninhados",  GUILayout.Width(100));
        GUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        filtroNome = EditorGUILayout.TextField("🔎 Filtrar por nome:", filtroNome);
        EditorGUILayout.Space(6);

        GUI.backgroundColor = cAzul;
        if (GUILayout.Button("🔍  VARRER A CENA AGORA", sBotaoVarrer))
            Varrer();
        GUI.backgroundColor = Color.white;
    }

    // ════════════════════════════════════════════════════════
    //  LISTA DE RESULTADOS
    // ════════════════════════════════════════════════════════
    void DesenharResultados()
    {
        int marcadosCount = marcados.Count(m => m);

        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("OBJETOS ENCONTRADOS", sTitulo);
        EditorGUILayout.Space(4);

        // Resumo
        var corResumo = new GUIStyle(EditorStyles.boldLabel)
        {
            normal = { textColor = cLaranja },
            fontSize = 11
        };
        GUILayout.Label($"  ⚠  {encontrados.Count} objeto(s) vazio(s) — {marcadosCount} marcado(s) para deletar", corResumo);
        EditorGUILayout.Space(4);

        // Botões de seleção rápida
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("✅ Marcar Todos",    GUILayout.Height(22))) MarcarTodos(true);
        if (GUILayout.Button("☐ Desmarcar Todos", GUILayout.Height(22))) MarcarTodos(false);
        if (GUILayout.Button("👁 Selecionar na Cena", GUILayout.Height(22))) SelecionarNaCena();
        GUILayout.EndHorizontal();

        EditorGUILayout.Space(4);

        // Lista scrollável
        scroll = GUILayout.BeginScrollView(scroll, GUILayout.MaxHeight(220));

        for (int i = 0; i < encontrados.Count; i++)
        {
            if (encontrados[i] == null) continue;

            bool  marcado = marcados[i];
            Color fundo   = marcado ? cFundoMarcado : cFundoItem;

            Rect itemRect = GUILayoutUtility.GetRect(0, 24, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(itemRect, fundo);

            // Borda lateral colorida
            EditorGUI.DrawRect(new Rect(itemRect.x, itemRect.y, 3, itemRect.height),
                marcado ? cVermelho : cAzul);

            // Checkbox
            Rect checkRect = new Rect(itemRect.x + 8, itemRect.y + 4, 16, 16);
            bool novoMarcado = EditorGUI.Toggle(checkRect, marcado);
            if (novoMarcado != marcado) marcados[i] = novoMarcado;

            // Ícone de hierarquia
            bool naRaiz = encontrados[i].transform.parent == null;
            string icone = naRaiz ? "📂" : "   📁";

            // Nome e caminho
            string caminho = ObterCaminho(encontrados[i]);
            Rect labelRect = new Rect(itemRect.x + 30, itemRect.y + 4, itemRect.width - 80, 16);
            EditorGUI.LabelField(labelRect, $"{icone} {caminho}", sLabel);

            // Botão "Pingar" (seleciona e dá ping na Hierarchy)
            Rect pingRect = new Rect(itemRect.xMax - 48, itemRect.y + 3, 44, 18);
            if (GUI.Button(pingRect, "ping", EditorStyles.miniButton))
            {
                Selection.activeGameObject = encontrados[i];
                EditorGUIUtility.PingObject(encontrados[i]);
            }

            // Linha divisória
            EditorGUI.DrawRect(new Rect(itemRect.x, itemRect.yMax - 1, itemRect.width, 1),
                new Color(0.25f, 0.25f, 0.35f));
        }

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  PAINEL DE AÇÃO
    // ════════════════════════════════════════════════════════
    void DesenharPainelAcao()
    {
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("AÇÃO", sTitulo);
        EditorGUILayout.Space(4);

        int marcadosCount = marcados.Count(m => m);

        EditorGUILayout.HelpBox(
            $"⚠ ATENÇÃO: {marcadosCount} objeto(s) serão DELETADOS permanentemente.\n" +
            "Use Ctrl+Z para desfazer após a exclusão.\n" +
            "Apenas objetos VAZIOS (sem scripts, componentes ou filhos) serão removidos.",
            MessageType.Warning);

        EditorGUILayout.Space(4);

        GUI.enabled = marcadosCount > 0;
        GUI.backgroundColor = marcadosCount > 0 ? cVermelho : Color.gray;

        if (GUILayout.Button($"🗑  DELETAR {marcadosCount} OBJETO(S) MARCADO(S)", sBotaoDeletar))
            DeletarMarcados();

        GUI.backgroundColor = Color.white;
        GUI.enabled = true;

        EditorGUILayout.Space(4);

        GUILayout.BeginHorizontal();
        GUI.backgroundColor = new Color(0.3f, 0.3f, 0.4f);
        if (GUILayout.Button("↺  Varrer Novamente", GUILayout.Height(26)))
            Varrer();
        if (GUILayout.Button("✕  Limpar Lista", GUILayout.Height(26)))
        {
            encontrados.Clear();
            marcados.Clear();
            escaneou = false;
        }
        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  CENA LIMPA
    // ════════════════════════════════════════════════════════
    void DesenharCenaLimpa()
    {
        GUILayout.BeginVertical(sSecao);
        var st = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 12,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = cVerde }
        };
        EditorGUILayout.Space(10);
        GUILayout.Label("✅  Cena limpa! Nenhum GameObject", st);
        GUILayout.Label("     vazio ou fantasma encontrado.", st);
        EditorGUILayout.Space(10);
        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  INSTRUÇÕES INICIAIS
    // ════════════════════════════════════════════════════════
    void DesenharInstrucoes()
    {
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("O QUE É UM OBJETO VAZIO?", sTitulo);
        EditorGUILayout.Space(4);

        string[] itens = {
            "✦ Possui apenas o componente Transform",
            "✦ Não tem filhos (childCount == 0)",
            "✦ Não tem SpriteRenderer",
            "✦ Não tem componentes de UI (Image, Text...)",
            "✦ Não tem Mesh, Collider, AudioSource...",
            "✦ Não tem scripts MonoBehaviour",
        };
        foreach (var item in itens)
            GUILayout.Label($"  {item}", sLabel);

        EditorGUILayout.Space(10);
        GUILayout.Label("COMO USAR", sTitulo);
        EditorGUILayout.Space(4);
        GUILayout.Label("  1. Clique em 🔍 VARRER A CENA AGORA", sLabel);
        GUILayout.Label("  2. Revise a lista de objetos encontrados", sLabel);
        GUILayout.Label("  3. Desmarque os que quiser preservar", sLabel);
        GUILayout.Label("  4. Clique em 🗑 DELETAR para limpar", sLabel);
        GUILayout.Label("  5. Use Ctrl+Z se quiser desfazer", sLabel);

        EditorGUILayout.Space(8);

        var stNota = new GUIStyle(EditorStyles.miniLabel)
        {
            wordWrap = true,
            normal   = { textColor = new Color(0.6f, 0.6f, 0.7f) }
        };
        GUILayout.Label(
            "💡 Nota: Todas as mensagens desta ferramenta usam LogWarning\n" +
            "(amarelo no Console) — nenhum erro vermelho é gerado.\n" +
            "Isso garante que o git e o CI/CD não sejam afetados.",
            stNota);

        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — VARRER
    // ════════════════════════════════════════════════════════
    void Varrer()
    {
        encontrados.Clear();
        marcados.Clear();
        escaneou = true;

        GameObject[] todos = FindObjectsOfType<GameObject>(includeInactive: true);

        foreach (GameObject go in todos)
        {
            if (!EhVazio(go))            continue;
            if (!PassaFiltroNome(go))    continue;
            if (!PassaFiltroHierarquia(go)) continue;

            encontrados.Add(go);
            marcados.Add(true); // marcado por padrão
        }

        // Ordena: raiz primeiro, depois por nome
        encontrados = encontrados
            .OrderBy(go => go.transform.parent != null)
            .ThenBy(go => go.name)
            .ToList();

        // ⚠ LogWarning — nunca LogError (não polui o console vermelho)
        if (encontrados.Count > 0)
            Debug.LogWarning(
                $"[🧹 Vassoureiro] Varredura concluída — {encontrados.Count} GameObject(s) vazio(s) " +
                $"encontrado(s) na cena \"{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}\".\n" +
                "Revise a lista na janela do Vassoureiro antes de deletar.");
        else
            Debug.LogWarning(
                "[🧹 Vassoureiro] Varredura concluída — Nenhum GameObject vazio encontrado. Cena limpa! ✅");

        Repaint();
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — DELETAR MARCADOS
    // ════════════════════════════════════════════════════════
    void DeletarMarcados()
    {
        List<GameObject> paraseDeletar = new List<GameObject>();
        for (int i = 0; i < encontrados.Count; i++)
            if (marcados[i] && encontrados[i] != null)
                paraseDeletar.Add(encontrados[i]);

        if (paraseDeletar.Count == 0)
        {
            EditorUtility.DisplayDialog("Nada selecionado",
                "Marque ao menos 1 objeto para deletar.", "OK");
            return;
        }

        bool confirmar = EditorUtility.DisplayDialog(
            "⚠  Confirmar Exclusão",
            $"Você está prestes a deletar {paraseDeletar.Count} GameObject(s) vazio(s):\n\n" +
            string.Join("\n", paraseDeletar.Take(10).Select(go => $"  • {ObterCaminho(go)}")) +
            (paraseDeletar.Count > 10 ? $"\n  ... e mais {paraseDeletar.Count - 10}" : "") +
            "\n\nEsta ação pode ser desfeita com Ctrl+Z.",
            "Sim, deletar",
            "Cancelar");

        if (!confirmar) return;

        // Registra undo em grupo
        Undo.SetCurrentGroupName("Vassoureiro — Deletar Vazios");
        int grupoUndo = Undo.GetCurrentGroup();

        int deletados = 0;
        foreach (GameObject go in paraseDeletar)
        {
            if (go == null) continue;
            Undo.DestroyObjectImmediate(go);
            deletados++;
        }

        Undo.CollapseUndoOperations(grupoUndo);

        // Marca cena como modificada
        EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        // ⚠ LogWarning — nunca LogError
        Debug.LogWarning(
            $"[🧹 Vassoureiro] {deletados} GameObject(s) vazio(s) removido(s). " +
            "Use Ctrl+Z para desfazer se necessário.");

        // Limpa a lista e varre novamente
        encontrados.Clear();
        marcados.Clear();
        Varrer();

        EditorUtility.DisplayDialog(
            "🧹 Limpeza Concluída!",
            $"{deletados} objeto(s) vazio(s) removido(s) da cena.\n\nUse Ctrl+Z para desfazer.",
            "OK");
    }

    // ════════════════════════════════════════════════════════
    //  UTILITÁRIOS
    // ════════════════════════════════════════════════════════

    // Retorna true se o GameObject tem APENAS Transform e nenhum filho
    bool EhVazio(GameObject go)
    {
        // Deve ter filhos? Então não é "fantasma"
        if (go.transform.childCount > 0) return false;

        // Coleta todos os componentes
        Component[] comps = go.GetComponents<Component>();

        // Só é vazio se o ÚNICO componente for Transform
        foreach (Component comp in comps)
        {
            if (comp == null)            continue; // componente quebrado — ignora
            if (comp is Transform)       continue; // Transform sempre existe — ok

            // Qualquer outro componente = não é vazio
            return false;
        }

        return true;
    }

    bool PassaFiltroNome(GameObject go)
    {
        if (string.IsNullOrEmpty(filtroNome)) return true;
        return go.name.ToLower().Contains(filtroNome.ToLower());
    }

    bool PassaFiltroHierarquia(GameObject go)
    {
        bool naRaiz = go.transform.parent == null;
        if (naRaiz   && !mostrarRaiz)   return false;
        if (!naRaiz  && !mostrarNested) return false;
        return true;
    }

    string ObterCaminho(GameObject go)
    {
        if (go == null) return "???";
        string path = go.name;
        Transform pai = go.transform.parent;
        int profundidade = 0;
        while (pai != null && profundidade < 3)
        {
            path = pai.name + "/" + path;
            pai = pai.parent;
            profundidade++;
        }
        if (pai != null) path = ".../" + path;
        return path;
    }

    void MarcarTodos(bool valor)
    {
        for (int i = 0; i < marcados.Count; i++)
            marcados[i] = valor;
    }

    void SelecionarNaCena()
    {
        Selection.objects = encontrados
            .Where(go => go != null)
            .Cast<Object>()
            .ToArray();
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

        sSecao = new GUIStyle(EditorStyles.helpBox)
        {
            padding = new RectOffset(10, 10, 8, 8)
        };

        sLabel = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(0.75f, 0.75f, 0.85f) }
        };

        sLabelPerigo = new GUIStyle(EditorStyles.miniLabel)
        {
            fontStyle = FontStyle.Bold,
            normal    = { textColor = cLaranja }
        };

        sBotaoVarrer = new GUIStyle(GUI.skin.button)
        {
            fontSize    = 12,
            fontStyle   = FontStyle.Bold,
            fixedHeight = 38,
            normal      = { textColor = Color.white }
        };

        sBotaoDeletar = new GUIStyle(GUI.skin.button)
        {
            fontSize    = 11,
            fontStyle   = FontStyle.Bold,
            fixedHeight = 36,
            normal      = { textColor = Color.white }
        };

        sBotaoNeutro = new GUIStyle(GUI.skin.button)
        {
            fontSize    = 10,
            fixedHeight = 26,
        };

        estilosOk = true;
    }
}
