using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// CentralizadorCenario - Ferramenta para centralizar e organizar todos os objetos do cenário.
/// Acesse por: Menu Unity > Ferramentas Sabrina > Centralizar Cenário
/// </summary>
public class CentralizadorCenario : EditorWindow
{
    // ─── Configurações ────────────────────────────────────────────────────────
    private float espacamentoHorizontal = 5f;
    private float espacamentoVertical   = 3f;
    private int   colunas               = 4;
    private bool  incluirFilhos         = false;
    private bool  corrigirEscala        = true;
    private float escalaAlvo            = 1f;
    private bool  corrigirZ             = true;
    private float zAlvo                 = 0f;
    private bool  mostrarPreview        = false;
    private Vector2 scroll;

    // ─── Objetos encontrados ──────────────────────────────────────────────────
    private List<GameObject> objetosRaiz = new List<GameObject>();

    [MenuItem("Ferramentas/🏙️ Centralizar Cenário")]
    public static void AbrirJanela()
    {
        var janela = GetWindow<CentralizadorCenario>("🏙️ Centralizar Cenário");
        janela.minSize = new Vector2(420, 580);
        janela.ScanCenario();
    }

    // ─── GUI ──────────────────────────────────────────────────────────────────
    private void OnGUI()
    {
        DrawCabecalho();
        DrawDivisor();
        DrawConfiguracoes();
        DrawDivisor();
        DrawListaObjetos();
        DrawDivisor();
        DrawBotoes();
    }

    private void DrawCabecalho()
    {
        GUIStyle titulo = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 18,
            alignment = TextAnchor.MiddleCenter
        };
        GUIStyle sub = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            wordWrap  = true
        };

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("🏙️ Centralizar Cenário", titulo);
        EditorGUILayout.LabelField("Organiza e centraliza todos os objetos da cena, corrigindo posições e escalas absurdas.", sub);
        EditorGUILayout.Space(8);
    }

    private void DrawDivisor()
    {
        Rect r = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(r, new Color(0.3f, 0.3f, 0.3f));
        EditorGUILayout.Space(4);
    }

    private void DrawConfiguracoes()
    {
        EditorGUILayout.LabelField("⚙️ Configurações de Layout", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Espaçamento Horizontal", GUILayout.Width(180));
        espacamentoHorizontal = EditorGUILayout.FloatField(espacamentoHorizontal);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Espaçamento Vertical", GUILayout.Width(180));
        espacamentoVertical = EditorGUILayout.FloatField(espacamentoVertical);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Colunas por Linha", GUILayout.Width(180));
        colunas = EditorGUILayout.IntSlider(colunas, 1, 10);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("⚙️ Correções", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        // Corrigir escala
        EditorGUILayout.BeginHorizontal();
        corrigirEscala = EditorGUILayout.Toggle(corrigirEscala, GUILayout.Width(20));
        EditorGUILayout.LabelField("Corrigir escala absurda / zerada", GUILayout.Width(230));
        if (corrigirEscala)
        {
            EditorGUILayout.LabelField("Escala alvo:", GUILayout.Width(75));
            escalaAlvo = EditorGUILayout.FloatField(escalaAlvo, GUILayout.Width(50));
        }
        EditorGUILayout.EndHorizontal();

        // Corrigir Z
        EditorGUILayout.BeginHorizontal();
        corrigirZ = EditorGUILayout.Toggle(corrigirZ, GUILayout.Width(20));
        EditorGUILayout.LabelField("Corrigir posição Z absurda / zerada", GUILayout.Width(230));
        if (corrigirZ)
        {
            EditorGUILayout.LabelField("Z alvo:", GUILayout.Width(75));
            zAlvo = EditorGUILayout.FloatField(zAlvo, GUILayout.Width(50));
        }
        EditorGUILayout.EndHorizontal();

        // Incluir filhos
        EditorGUILayout.BeginHorizontal();
        incluirFilhos = EditorGUILayout.Toggle(incluirFilhos, GUILayout.Width(20));
        EditorGUILayout.LabelField("Incluir objetos filhos no grid", GUILayout.Width(230));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);
    }

    private void DrawListaObjetos()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"📋 Objetos encontrados na cena: {objetosRaiz.Count}", EditorStyles.boldLabel);
        if (GUILayout.Button("🔄 Atualizar", GUILayout.Width(90))) ScanCenario();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4);

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(160));

        foreach (var obj in objetosRaiz)
        {
            if (obj == null) continue;
            EditorGUILayout.BeginHorizontal();

            // Ícone de alerta se posição ou escala estiver estranha
            bool posRuim   = TemPosicaoAbsurda(obj.transform.position);
            bool escalaRuim = TemEscalaAbsurda(obj.transform.localScale);
            string icone = (posRuim || escalaRuim) ? "⚠️" : "✅";

            EditorGUILayout.LabelField(icone, GUILayout.Width(20));
            EditorGUILayout.ObjectField(obj, typeof(GameObject), true, GUILayout.Width(200));

            Vector3 pos = obj.transform.position;
            EditorGUILayout.LabelField($"Pos: ({pos.x:F0}, {pos.y:F0}, {pos.z:F0})", EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawBotoes()
    {
        EditorGUILayout.Space(8);

        // Aviso
        int problemCount = 0;
        foreach (var obj in objetosRaiz)
        {
            if (obj == null) continue;
            if (TemPosicaoAbsurda(obj.transform.position) || TemEscalaAbsurda(obj.transform.localScale))
                problemCount++;
        }

        if (problemCount > 0)
        {
            EditorGUILayout.HelpBox($"⚠️ {problemCount} objeto(s) com posição ou escala fora do normal detectados!", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox("✅ Todos os objetos parecem estar bem posicionados.", MessageType.Info);
        }

        EditorGUILayout.Space(6);

        GUIStyle botaoVerde = new GUIStyle(GUI.skin.button)
        {
            fontSize    = 13,
            fontStyle   = FontStyle.Bold,
            fixedHeight = 40
        };

        GUIStyle botaoAzul = new GUIStyle(GUI.skin.button)
        {
            fontSize    = 12,
            fixedHeight = 34
        };

        if (GUILayout.Button("🏙️  CENTRALIZAR E ORGANIZAR TUDO", botaoVerde))
        {
            if (EditorUtility.DisplayDialog(
                "Centralizar Cenário",
                $"Isso vai reorganizar {objetosRaiz.Count} objeto(s) na cena.\n\nUm Undo ficará disponível depois.\n\nContinuar?",
                "Sim, centralizar!",
                "Cancelar"))
            {
                CentralizarTudo();
            }
        }

        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("🔧 Só corrigir escala/Z", botaoAzul))
        {
            CorrigirApenasEscalaEZ();
        }

        if (GUILayout.Button("🎯 Levar câmera ao centro", botaoAzul))
        {
            LevarCameraAoCentro();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);
    }

    // ─── Lógica Principal ─────────────────────────────────────────────────────

    private void ScanCenario()
    {
        objetosRaiz.Clear();

        GameObject[] todos = incluirFilhos
            ? GameObject.FindObjectsOfType<GameObject>()
            : GetRootObjects();

        foreach (var obj in todos)
        {
            if (obj == null) continue;
            // Ignorar câmeras, lights e sistemas internos
            if (obj.name == "EventSystem") continue;
            if (obj.GetComponent<Camera>() != null) continue;
            if (obj.GetComponent<Light>() != null) continue;
            objetosRaiz.Add(obj);
        }

        Repaint();
    }

    private GameObject[] GetRootObjects()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        return scene.GetRootGameObjects();
    }

    private void CentralizarTudo()
    {
        Undo.SetCurrentGroupName("Centralizar Cenário");
        int group = Undo.GetCurrentGroup();

        List<GameObject> alvos = new List<GameObject>();
        foreach (var obj in objetosRaiz)
        {
            if (obj == null) continue;
            if (obj.name == "EventSystem") continue;
            if (obj.GetComponent<Camera>() != null) continue;
            if (obj.GetComponent<Light>() != null) continue;
            alvos.Add(obj);
        }

        int total  = alvos.Count;
        int coluna = 0;
        int linha  = 0;

        for (int i = 0; i < total; i++)
        {
            var obj = alvos[i];
            Undo.RecordObject(obj.transform, "Mover " + obj.name);

            // Calcular grid centrado
            float totalLargura = (Mathf.Min(total, colunas) - 1) * espacamentoHorizontal;
            float totalAltura  = (Mathf.Ceil((float)total / colunas) - 1) * espacamentoVertical;

            float x = (coluna * espacamentoHorizontal) - (totalLargura / 2f);
            float y = -(linha  * espacamentoVertical)  + (totalAltura  / 2f);
            float z = corrigirZ ? zAlvo : obj.transform.position.z;

            obj.transform.position = new Vector3(x, y, z);

            // Corrigir escala
            if (corrigirEscala && TemEscalaAbsurda(obj.transform.localScale))
            {
                obj.transform.localScale = new Vector3(escalaAlvo, escalaAlvo, 1f);
            }

            coluna++;
            if (coluna >= colunas)
            {
                coluna = 0;
                linha++;
            }

            EditorUtility.DisplayProgressBar("Centralizando...", obj.name, (float)i / total);
        }

        EditorUtility.ClearProgressBar();
        Undo.CollapseUndoOperations(group);

        Debug.Log($"[CentralizadorCenario] ✅ {total} objetos centralizados e organizados!");
        EditorUtility.DisplayDialog("Concluído!", $"✅ {total} objetos foram centralizados e organizados na cena!\n\nUse Ctrl+Z para desfazer se precisar.", "OK");

        ScanCenario();
        SceneView.RepaintAll();
    }

    private void CorrigirApenasEscalaEZ()
    {
        Undo.SetCurrentGroupName("Corrigir Escala e Z");
        int group = Undo.GetCurrentGroup();
        int count = 0;

        foreach (var obj in objetosRaiz)
        {
            if (obj == null) continue;

            bool alterou = false;
            Undo.RecordObject(obj.transform, "Corrigir " + obj.name);

            if (corrigirEscala && TemEscalaAbsurda(obj.transform.localScale))
            {
                obj.transform.localScale = new Vector3(escalaAlvo, escalaAlvo, 1f);
                alterou = true;
            }

            if (corrigirZ && (Mathf.Abs(obj.transform.position.z) > 100f || float.IsNaN(obj.transform.position.z)))
            {
                Vector3 pos = obj.transform.position;
                obj.transform.position = new Vector3(pos.x, pos.y, zAlvo);
                alterou = true;
            }

            if (alterou) count++;
        }

        Undo.CollapseUndoOperations(group);

        Debug.Log($"[CentralizadorCenario] 🔧 {count} objetos tiveram escala/Z corrigidos.");
        EditorUtility.DisplayDialog("Correção aplicada!", $"🔧 {count} objetos tiveram escala e/ou Z corrigidos.\n\nUse Ctrl+Z para desfazer.", "OK");

        ScanCenario();
        SceneView.RepaintAll();
    }

    private void LevarCameraAoCentro()
    {
        SceneView sv = SceneView.lastActiveSceneView;
        if (sv != null)
        {
            sv.LookAt(Vector3.zero, sv.rotation, 20f);
            sv.Repaint();
            Debug.Log("[CentralizadorCenario] 🎯 Câmera levada ao centro (0,0,0).");
        }
        else
        {
            EditorUtility.DisplayDialog("Atenção", "Nenhuma Scene View ativa encontrada.", "OK");
        }
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private bool TemPosicaoAbsurda(Vector3 pos)
    {
        return Mathf.Abs(pos.x) > 500f
            || Mathf.Abs(pos.y) > 500f
            || Mathf.Abs(pos.z) > 500f
            || float.IsNaN(pos.x)
            || float.IsNaN(pos.y)
            || float.IsNaN(pos.z);
    }

    private bool TemEscalaAbsurda(Vector3 escala)
    {
        return escala.x == 0f
            || escala.y == 0f
            || Mathf.Abs(escala.x) > 50f
            || Mathf.Abs(escala.y) > 50f
            || float.IsNaN(escala.x)
            || float.IsNaN(escala.y);
    }
}
