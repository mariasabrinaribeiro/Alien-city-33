using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

// ============================================================
// OrganizadorHierarquia.cs  —  Ferramenta de Editor
//
// Abre em: Ferramentas → Organizador de Hierarquia
//
// Funcionalidades:
//   • Agrupar objetos selecionados em uma pasta (grupo)
//   • Agrupar TUDO por tipo de componente
//   • Agrupar TUDO por Tag
//   • Ordenar filhos A→Z ou Z→A
//   • Renomear em lote (prefixo, sufixo, numeração)
//   • Limpar GameObjects vazios da cena
// ============================================================

public class OrganizadorHierarquia : EditorWindow
{
    // ── Estado da janela ─────────────────────────────────────
    private int    abaAtiva      = 0;
    private string[] abas        = { "📁 Agrupar", "🔤 Renomear", "🧹 Limpar" };

    // Agrupar
    private string nomeGrupo     = "Grupo";
    private bool   agruparPorTipo = true;

    // Renomear
    private string prefixo       = "";
    private string sufixo        = "";
    private bool   numerarObjetos = true;
    private int    iniciarEm     = 1;
    private string separador     = "_";

    // Ordenar
    private bool ordemAZ         = true;

    // UI
    private Vector2 scroll       = Vector2.zero;
    private GUIStyle estiloTitulo;
    private GUIStyle estiloBotao;
    private GUIStyle estiloSecao;
    private bool estilosCarregados = false;

    // ── Abrir janela ─────────────────────────────────────────
    [MenuItem("Ferramentas/Organizador de Hierarquia  %#o")]
    public static void AbrirJanela()
    {
        OrganizadorHierarquia janela = GetWindow<OrganizadorHierarquia>("📐 Organizador");
        janela.minSize = new Vector2(300, 520);
        janela.Show();
    }

    // ── Carregar estilos ─────────────────────────────────────
    void CarregarEstilos()
    {
        if (estilosCarregados) return;

        estiloTitulo = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 13,
            alignment = TextAnchor.MiddleCenter
        };
        estiloTitulo.normal.textColor = new Color(0.4f, 0.85f, 1f);

        estiloBotao = new GUIStyle(GUI.skin.button)
        {
            fontSize   = 11,
            fixedHeight = 32,
            fontStyle  = FontStyle.Bold
        };

        estiloSecao = new GUIStyle(EditorStyles.helpBox)
        {
            padding = new RectOffset(10, 10, 8, 8)
        };

        estilosCarregados = true;
    }

    // ── Desenha a janela ─────────────────────────────────────
    void OnGUI()
    {
        CarregarEstilos();
        scroll = GUILayout.BeginScrollView(scroll);

        // Cabeçalho
        EditorGUILayout.Space(6);
        GUILayout.Label("ORGANIZADOR DE HIERARQUIA", estiloTitulo);
        EditorGUILayout.Space(2);
        DrawLinha();

        // Abas
        abaAtiva = GUILayout.Toolbar(abaAtiva, abas, GUILayout.Height(28));
        EditorGUILayout.Space(8);

        switch (abaAtiva)
        {
            case 0: DesenharAbaAgrupar();  break;
            case 1: DesenharAbaRenomear(); break;
            case 2: DesenharAbaLimpar();   break;
        }

        GUILayout.EndScrollView();
    }

    // ════════════════════════════════════════════════════════
    //  ABA 1 — AGRUPAR
    // ════════════════════════════════════════════════════════
    void DesenharAbaAgrupar()
    {
        // ── Agrupar Selecionados ──────────────────────────
        GUILayout.BeginVertical(estiloSecao);
        GUILayout.Label("📁 Agrupar Selecionados em Pasta", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        nomeGrupo = EditorGUILayout.TextField("Nome do grupo:", nomeGrupo);
        EditorGUILayout.Space(4);

        int qtd = Selection.gameObjects?.Length ?? 0;
        EditorGUILayout.HelpBox($"{qtd} objeto(s) selecionado(s)", MessageType.Info);
        EditorGUILayout.Space(4);

        GUI.enabled = qtd > 0;
        if (GUILayout.Button("📁  Criar Grupo com Selecionados", estiloBotao))
            AgruparSelecionados();
        GUI.enabled = true;

        GUILayout.EndVertical();
        EditorGUILayout.Space(10);

        // ── Agrupar por Tipo ──────────────────────────────
        GUILayout.BeginVertical(estiloSecao);
        GUILayout.Label("🏷️ Auto-Agrupar Cena Inteira", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        if (GUILayout.Button("🔵  Agrupar por Tipo de Componente", estiloBotao))
            AgruparPorTipo();

        EditorGUILayout.Space(4);

        if (GUILayout.Button("🟢  Agrupar por Tag", estiloBotao))
            AgruparPorTag();

        GUILayout.EndVertical();
        EditorGUILayout.Space(10);

        // ── Ordenar filhos ────────────────────────────────
        GUILayout.BeginVertical(estiloSecao);
        GUILayout.Label("🔤 Ordenar Filhos do Selecionado", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        ordemAZ = EditorGUILayout.Toggle("Ordem A → Z (desmarque para Z→A)", ordemAZ);
        EditorGUILayout.Space(4);

        GUI.enabled = Selection.activeGameObject != null;
        if (GUILayout.Button(ordemAZ ? "🔤  Ordenar Filhos A → Z" : "🔤  Ordenar Filhos Z → A", estiloBotao))
            OrdenarFilhos(Selection.activeGameObject, ordemAZ);
        GUI.enabled = true;

        EditorGUILayout.Space(4);
        if (GUILayout.Button("🔤  Ordenar TUDO na Cena (raiz A→Z)", estiloBotao))
            OrdenarTodaRaiz();

        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  ABA 2 — RENOMEAR
    // ════════════════════════════════════════════════════════
    void DesenharAbaRenomear()
    {
        GUILayout.BeginVertical(estiloSecao);
        GUILayout.Label("✏️ Renomear em Lote", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        prefixo       = EditorGUILayout.TextField("Prefixo:", prefixo);
        sufixo        = EditorGUILayout.TextField("Sufixo:", sufixo);
        numerarObjetos = EditorGUILayout.Toggle("Adicionar numeração:", numerarObjetos);

        if (numerarObjetos)
        {
            iniciarEm  = EditorGUILayout.IntField("Começar em:", iniciarEm);
            separador  = EditorGUILayout.TextField("Separador:", separador);
        }

        EditorGUILayout.Space(6);

        // Preview
        int qtdR = Selection.gameObjects?.Length ?? 0;
        if (qtdR > 0 && Selection.gameObjects[0] != null)
        {
            string nomeBase   = Selection.gameObjects[0].name;
            string preview    = MontarNome(nomeBase, 0);
            EditorGUILayout.HelpBox($"Preview: \"{preview}\"  ({qtdR} objeto(s))", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox("Selecione objetos na Hierarchy para renomear.", MessageType.Warning);
        }

        EditorGUILayout.Space(4);
        GUI.enabled = qtdR > 0;
        if (GUILayout.Button("✏️  Renomear Selecionados", estiloBotao))
            RenomearSelecionados();
        GUI.enabled = true;

        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  ABA 3 — LIMPAR
    // ════════════════════════════════════════════════════════
    void DesenharAbaLimpar()
    {
        GUILayout.BeginVertical(estiloSecao);
        GUILayout.Label("🧹 Limpeza da Cena", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        EditorGUILayout.HelpBox(
            "Remove GameObjects completamente vazios (sem componentes e sem filhos).",
            MessageType.Info);
        EditorGUILayout.Space(4);

        if (GUILayout.Button("🧹  Remover GameObjects Vazios", estiloBotao))
            RemoverVazios();

        EditorGUILayout.Space(10);
        DrawLinha();
        EditorGUILayout.Space(6);

        GUILayout.Label("📋 Estatísticas da Cena", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        if (GUILayout.Button("📊  Ver Contagem de Objetos", estiloBotao))
            MostrarEstatisticas();

        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — AGRUPAR SELECIONADOS
    // ════════════════════════════════════════════════════════
    void AgruparSelecionados()
    {
        GameObject[] selecionados = Selection.gameObjects;
        if (selecionados == null || selecionados.Length == 0) return;

        // Cria pasta vazia
        GameObject pasta = new GameObject(string.IsNullOrEmpty(nomeGrupo) ? "Grupo" : nomeGrupo);
        Undo.RegisterCreatedObjectUndo(pasta, "Criar Grupo");

        // Usa o pai do primeiro objeto como referência
        Transform paiRef = selecionados[0].transform.parent;
        if (paiRef != null)
            pasta.transform.SetParent(paiRef, false);

        // Mínimo sibling index dos selecionados
        int minIndex = int.MaxValue;
        foreach (var go in selecionados)
            if (go.transform.GetSiblingIndex() < minIndex)
                minIndex = go.transform.GetSiblingIndex();

        pasta.transform.SetSiblingIndex(minIndex);

        // Move todos para dentro da pasta
        foreach (GameObject go in selecionados)
        {
            Undo.SetTransformParent(go.transform, pasta.transform, "Mover para grupo");
            go.transform.SetParent(pasta.transform, true);
        }

        Selection.activeGameObject = pasta;
        Debug.Log($"[Organizador] Grupo \"{pasta.name}\" criado com {selecionados.Length} objetos.");
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — AGRUPAR POR TIPO
    // ════════════════════════════════════════════════════════
    void AgruparPorTipo()
    {
        bool confirmar = EditorUtility.DisplayDialog(
            "Agrupar por Tipo",
            "Isso vai organizar TODOS os objetos da cena em grupos por tipo de componente principal.\n\nDeseja continuar?",
            "Sim", "Cancelar");
        if (!confirmar) return;

        // Mapa: nomeTipo → lista de objetos
        Dictionary<string, List<GameObject>> grupos = new Dictionary<string, List<GameObject>>();

        GameObject[] todos = FindObjectsOfType<GameObject>();
        foreach (GameObject go in todos)
        {
            if (go.transform.parent != null) continue; // só raiz
            string tipo = ObterTipoLabel(go);
            if (!grupos.ContainsKey(tipo))
                grupos[tipo] = new List<GameObject>();
            grupos[tipo].Add(go);
        }

        // Cria pasta para cada tipo e move os objetos
        foreach (var par in grupos)
        {
            // Não cria pasta se só tem 1 objeto do tipo e já é pasta vazia
            string nomePasta = $"--- {par.Key} ---";

            // Verifica se pasta já existe
            GameObject pastaExistente = GameObject.Find(nomePasta);
            GameObject pasta = pastaExistente ?? new GameObject(nomePasta);

            if (pastaExistente == null)
                Undo.RegisterCreatedObjectUndo(pasta, "Agrupar por tipo");

            foreach (GameObject go in par.Value)
            {
                if (go == pasta) continue;
                Undo.SetTransformParent(go.transform, pasta.transform, "Mover para grupo tipo");
                go.transform.SetParent(pasta.transform, true);
            }
        }

        Debug.Log($"[Organizador] Agrupado por tipo: {grupos.Count} grupo(s) criado(s).");
        EditorUtility.DisplayDialog("Concluído! ✔",
            $"{grupos.Count} grupo(s) criados por tipo de componente.", "OK");
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — AGRUPAR POR TAG
    // ════════════════════════════════════════════════════════
    void AgruparPorTag()
    {
        bool confirmar = EditorUtility.DisplayDialog(
            "Agrupar por Tag",
            "Isso vai organizar TODOS os objetos da cena em grupos pela Tag de cada objeto.\n\nDeseja continuar?",
            "Sim", "Cancelar");
        if (!confirmar) return;

        Dictionary<string, List<GameObject>> grupos = new Dictionary<string, List<GameObject>>();

        GameObject[] todos = FindObjectsOfType<GameObject>();
        foreach (GameObject go in todos)
        {
            if (go.transform.parent != null) continue;
            string tag = go.tag;
            if (!grupos.ContainsKey(tag))
                grupos[tag] = new List<GameObject>();
            grupos[tag].Add(go);
        }

        foreach (var par in grupos)
        {
            string nomePasta = $"[{par.Key}]";
            GameObject pastaExistente = GameObject.Find(nomePasta);
            GameObject pasta = pastaExistente ?? new GameObject(nomePasta);

            if (pastaExistente == null)
                Undo.RegisterCreatedObjectUndo(pasta, "Agrupar por tag");

            foreach (GameObject go in par.Value)
            {
                if (go == pasta) continue;
                Undo.SetTransformParent(go.transform, pasta.transform, "Mover para grupo tag");
                go.transform.SetParent(pasta.transform, true);
            }
        }

        Debug.Log($"[Organizador] Agrupado por Tag: {grupos.Count} grupo(s) criado(s).");
        EditorUtility.DisplayDialog("Concluído! ✔",
            $"{grupos.Count} grupo(s) criados por Tag.", "OK");
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — ORDENAR FILHOS
    // ════════════════════════════════════════════════════════
    void OrdenarFilhos(GameObject pai, bool az)
    {
        if (pai == null) return;

        List<Transform> filhos = new List<Transform>();
        foreach (Transform f in pai.transform)
            filhos.Add(f);

        filhos = az
            ? filhos.OrderBy(f => f.name).ToList()
            : filhos.OrderByDescending(f => f.name).ToList();

        for (int i = 0; i < filhos.Count; i++)
        {
            Undo.RegisterCompleteObjectUndo(filhos[i], "Ordenar filhos");
            filhos[i].SetSiblingIndex(i);
        }

        Debug.Log($"[Organizador] \"{pai.name}\" — {filhos.Count} filhos ordenados {(az ? "A→Z" : "Z→A")}.");
    }

    void OrdenarTodaRaiz()
    {
        GameObject[] raiz = FindObjectsOfType<GameObject>()
            .Where(go => go.transform.parent == null)
            .OrderBy(go => go.name)
            .ToArray();

        for (int i = 0; i < raiz.Length; i++)
        {
            Undo.RegisterCompleteObjectUndo(raiz[i].transform, "Ordenar raiz");
            raiz[i].transform.SetSiblingIndex(i);
        }

        Debug.Log($"[Organizador] Raiz ordenada A→Z: {raiz.Length} objetos.");
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — RENOMEAR EM LOTE
    // ════════════════════════════════════════════════════════
    void RenomearSelecionados()
    {
        GameObject[] selecionados = Selection.gameObjects;
        if (selecionados == null || selecionados.Length == 0) return;

        for (int i = 0; i < selecionados.Length; i++)
        {
            Undo.RegisterCompleteObjectUndo(selecionados[i], "Renomear em lote");
            selecionados[i].name = MontarNome(selecionados[i].name, i);
        }

        Debug.Log($"[Organizador] {selecionados.Length} objeto(s) renomeados.");
    }

    string MontarNome(string nomeBase, int indice)
    {
        string numero = numerarObjetos
            ? $"{separador}{(iniciarEm + indice).ToString()}"
            : "";
        return $"{prefixo}{nomeBase}{numero}{sufixo}";
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — LIMPAR VAZIOS
    // ════════════════════════════════════════════════════════
    void RemoverVazios()
    {
        GameObject[] todos = FindObjectsOfType<GameObject>();
        int removidos = 0;

        foreach (GameObject go in todos)
        {
            if (go == null) continue;
            bool semFilhos     = go.transform.childCount == 0;
            bool semComponentes = go.GetComponents<Component>().Length <= 1; // só Transform

            if (semFilhos && semComponentes)
            {
                Undo.DestroyObjectImmediate(go);
                removidos++;
            }
        }

        string msg = removidos > 0
            ? $"{removidos} objeto(s) vazio(s) removido(s)!\n\nUse Ctrl+Z para desfazer."
            : "Nenhum GameObject vazio encontrado na cena.";

        EditorUtility.DisplayDialog("Limpeza ✔", msg, "OK");
        Debug.Log($"[Organizador] {removidos} objeto(s) vazio(s) removidos.");
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — ESTATÍSTICAS
    // ════════════════════════════════════════════════════════
    void MostrarEstatisticas()
    {
        GameObject[] todos   = FindObjectsOfType<GameObject>();
        int total            = todos.Length;
        int naRaiz           = todos.Count(go => go.transform.parent == null);
        int comSprite        = todos.Count(go => go.GetComponent<SpriteRenderer>() != null);
        int comCamera        = todos.Count(go => go.GetComponent<Camera>() != null);
        int comColider       = todos.Count(go => go.GetComponent<Collider2D>() != null || go.GetComponent<Collider>() != null);
        int vazios           = todos.Count(go => go.transform.childCount == 0 && go.GetComponents<Component>().Length <= 1);

        EditorUtility.DisplayDialog("📊 Estatísticas da Cena",
            $"Total de GameObjects:   {total}\n" +
            $"Na raiz (sem pai):      {naRaiz}\n" +
            $"Com SpriteRenderer:     {comSprite}\n" +
            $"Com Camera:             {comCamera}\n" +
            $"Com Collider:           {comColider}\n" +
            $"Vazios (sem uso):       {vazios}",
            "OK");
    }

    // ════════════════════════════════════════════════════════
    //  UTILITÁRIO — Label do tipo principal do objeto
    // ════════════════════════════════════════════════════════
    string ObterTipoLabel(GameObject go)
    {
        if (go.GetComponent<Camera>())           return "Cameras";
        if (go.GetComponent<Light>())            return "Luzes";
        if (go.GetComponent<SpriteRenderer>())   return "Sprites";
        if (go.GetComponent<Canvas>())           return "Canvas_UI";
        if (go.GetComponent<AudioSource>())      return "Audio";
        if (go.GetComponent<ParticleSystem>())   return "Particulas";
        if (go.GetComponent<Collider2D>())       return "Colisores2D";
        if (go.GetComponent<Collider>())         return "Colisores3D";
        if (go.GetComponent<Rigidbody2D>())      return "Fisicos2D";
        if (go.GetComponent<Rigidbody>())        return "Fisicos3D";
        if (go.GetComponent<Animator>())         return "Animados";
        return "Outros";
    }

    // ── Linha divisória ───────────────────────────────────
    void DrawLinha()
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(rect, new Color(0.4f, 0.85f, 1f, 0.3f));
        EditorGUILayout.Space(2);
    }

    // Atualiza preview em tempo real
    void OnSelectionChange() => Repaint();
}
