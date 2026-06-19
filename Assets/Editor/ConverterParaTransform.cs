using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.Generic;

// ============================================================
// ConverterParaTransform.cs  —  Ferramenta de Editor
//
// Opções disponíveis em: Ferramentas → Converter RectTransform
//   1. Converter Selecionado(s)  — converte 1 ou vários selecionados
//   2. Converter TODOS da Cena   — converte todos com RectTransform
//
// Cada conversão:
//   • RectTransform  → Transform normal
//   • Image (UI)     → SpriteRenderer (2D)
//   • CanvasRenderer → removido
//   • Posição resetada para (0, 0, 0)
//   • Escala resetada  para (1, 1, 1)
// ============================================================

public class ConverterParaTransform : EditorWindow
{
    // ════════════════════════════════════════════════════════
    //  OPÇÃO 1 — Converter objeto(s) selecionado(s)
    // ════════════════════════════════════════════════════════
    [MenuItem("Ferramentas/Converter RectTransform/► Converter Selecionado(s)")]
    static void ConverterSelecionados()
    {
        GameObject[] selecionados = Selection.gameObjects;

        if (selecionados == null || selecionados.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "Nenhum objeto selecionado",
                "Selecione um ou mais GameObjects na Hierarchy antes de usar esta ferramenta.",
                "OK");
            return;
        }

        // Filtra apenas os que têm RectTransform
        List<GameObject> comRect = new List<GameObject>();
        foreach (GameObject go in selecionados)
            if (go.GetComponent<RectTransform>() != null)
                comRect.Add(go);

        if (comRect.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Sem RectTransform",
                "Nenhum dos objetos selecionados possui RectTransform.",
                "OK");
            return;
        }

        bool confirmar = EditorUtility.DisplayDialog(
            "Converter Selecionado(s)",
            $"Serão convertidos {comRect.Count} objeto(s):\n\n" +
            ListarNomes(comRect) +
            "\n• RectTransform → Transform\n" +
            "• Image → SpriteRenderer\n" +
            "• Posição → (0, 0, 0)\n" +
            "• Escala  → (1, 1, 1)\n\n" +
            "Deseja continuar?",
            "Sim, converter",
            "Cancelar");

        if (!confirmar) return;

        int convertidos = 0;
        foreach (GameObject go in comRect)
        {
            Undo.RegisterFullObjectHierarchyUndo(go, "Converter RectTransform → Transform");
            ExecutarConversao(go);
            convertidos++;
        }

        EditorUtility.DisplayDialog(
            "Concluído! ✔",
            $"{convertidos} objeto(s) convertidos com sucesso!\n\nUse Ctrl+Z para desfazer.",
            "OK");
    }

    [MenuItem("Ferramentas/Converter RectTransform/► Converter Selecionado(s)", true)]
    static bool ValidarSelecionados() => Selection.gameObjects != null && Selection.gameObjects.Length > 0;


    // ════════════════════════════════════════════════════════
    //  OPÇÃO 2 — Converter TODOS da cena
    // ════════════════════════════════════════════════════════
    [MenuItem("Ferramentas/Converter RectTransform/► Converter TODOS da Cena")]
    static void ConverterTodos()
    {
        // Busca todos os RectTransforms na cena inteira
        RectTransform[] todosRect = Object.FindObjectsOfType<RectTransform>();

        if (todosRect == null || todosRect.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "Nenhum RectTransform encontrado",
                "A cena não possui objetos com RectTransform.",
                "OK");
            return;
        }

        List<GameObject> lista = new List<GameObject>();
        foreach (RectTransform rt in todosRect)
            lista.Add(rt.gameObject);

        bool confirmar = EditorUtility.DisplayDialog(
            "Converter TODOS da Cena",
            $"Foram encontrados {lista.Count} objeto(s) com RectTransform:\n\n" +
            ListarNomes(lista) +
            "\nTodos serão convertidos:\n" +
            "• RectTransform → Transform\n" +
            "• Image → SpriteRenderer\n" +
            "• Posição → (0, 0, 0)\n" +
            "• Escala  → (1, 1, 1)\n\n" +
            "⚠ Esta ação afeta a cena inteira!\nDeseja continuar?",
            "Sim, converter TODOS",
            "Cancelar");

        if (!confirmar) return;

        int convertidos = 0;
        foreach (GameObject go in lista)
        {
            // Pula se já foi destruído (filho de outro convertido)
            if (go == null) continue;

            Undo.RegisterFullObjectHierarchyUndo(go, "Converter TODOS");
            ExecutarConversao(go);
            convertidos++;
        }

        EditorUtility.DisplayDialog(
            "Concluído! ✔",
            $"{convertidos} objeto(s) convertidos na cena!\n\nUse Ctrl+Z para desfazer.",
            "OK");
    }


    // ════════════════════════════════════════════════════════
    //  LÓGICA CENTRAL DE CONVERSÃO (reutilizada pelas opções)
    // ════════════════════════════════════════════════════════
    static void ExecutarConversao(GameObject original)
    {
        if (original == null) return;

        Transform paiAtual     = original.transform.parent;
        string    nomeOriginal = original.name;

        // Salva dados do Image (se existir)
        Image  imgOriginal    = original.GetComponent<Image>();
        Sprite spriteOriginal = null;
        Color  corOriginal    = Color.white;

        if (imgOriginal != null)
        {
            spriteOriginal = imgOriginal.sprite;
            corOriginal    = imgOriginal.color;
        }

        // 1. Cria novo GameObject com Transform normal
        GameObject novo = new GameObject(nomeOriginal);
        Undo.RegisterCreatedObjectUndo(novo, "Novo objeto com Transform");

        if (paiAtual != null)
            novo.transform.SetParent(paiAtual, worldPositionStays: false);

        // Posição e escala resetadas (Canvas usa pixels — não serve no mundo 2D)
        novo.transform.position   = Vector3.zero;
        novo.transform.rotation   = Quaternion.identity;
        novo.transform.localScale = Vector3.one;

        // 2. Image → SpriteRenderer
        if (imgOriginal != null && spriteOriginal != null)
        {
            SpriteRenderer sr = novo.AddComponent<SpriteRenderer>();
            sr.sprite       = spriteOriginal;
            sr.color        = corOriginal;
            sr.sortingOrder = 0;
        }

        // 3. Copia demais componentes (ignora os exclusivos de UI)
        Component[] componentes = original.GetComponents<Component>();
        foreach (Component comp in componentes)
        {
            if (comp is Transform)      continue;
            if (comp is Image)          continue;
            if (comp is CanvasRenderer) continue;

            ComponentUtility.CopyComponent(comp);
            ComponentUtility.PasteComponentAsNew(novo);
        }

        // 4. Transfere filhos
        List<Transform> filhos = new List<Transform>();
        foreach (Transform filho in original.transform)
            filhos.Add(filho);

        foreach (Transform filho in filhos)
        {
            Undo.SetTransformParent(filho, novo.transform, "Mover filho");
            filho.SetParent(novo.transform, worldPositionStays: true);
        }

        // 5. Mantém posição na hierarquia
        novo.transform.SetSiblingIndex(original.transform.GetSiblingIndex());

        // 6. Seleciona e destrói o antigo
        Selection.activeGameObject = novo;
        Undo.DestroyObjectImmediate(original);

        Debug.Log($"[ConverterParaTransform] \"{nomeOriginal}\" ✔");
    }


    // ════════════════════════════════════════════════════════
    //  UTILITÁRIO — Lista nomes (máx. 10 para não lotar dialog)
    // ════════════════════════════════════════════════════════
    static string ListarNomes(List<GameObject> lista)
    {
        int mostrar = Mathf.Min(lista.Count, 10);
        string resultado = "";
        for (int i = 0; i < mostrar; i++)
            resultado += $"  • {lista[i].name}\n";
        if (lista.Count > 10)
            resultado += $"  ... e mais {lista.Count - 10} objeto(s)\n";
        return resultado;
    }
}
