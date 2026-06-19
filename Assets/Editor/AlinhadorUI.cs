using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

// ============================================================
// AlinhadorUI.cs  —  Ferramenta de Editor
//
// Abre em: Ferramentas → Alinhador & Distribuidor UI  (Ctrl+Shift+A)
//
// Funcionalidades:
//   • Alinhar: Esquerda, Centro H, Direita, Topo, Centro V, Base
//   • Distribuir: espaçamento igual H e V
//   • Igualar: Largura, Altura ou Tamanho completo
//   • Nudge pixel-perfect com seta e valor customizável
//   • Espaçamento fixo entre elementos (gap em px)
//   • Funciona com RectTransform (UI) e Transform (2D sprites)
//   • Suporte total a Ctrl+Z
// ============================================================

public class AlinhadorUI : EditorWindow
{
    // ── Estado ───────────────────────────────────────────────
    private float   nudgePx     = 1f;
    private float   gapPx       = 10f;
    private bool    usarGapFixo = false;
    private Vector2 scroll;

    // ── Estilos ──────────────────────────────────────────────
    private GUIStyle sBotao, sBotaoAtivo, sTitulo, sSecao, sGrupo, sLabel;
    private bool     estilosOk = false;

    // ── Cores ────────────────────────────────────────────────
    private static readonly Color cAzul    = new Color(0.18f, 0.52f, 1.00f);
    private static readonly Color cVerde   = new Color(0.15f, 0.78f, 0.45f);
    private static readonly Color cLaranja = new Color(1.00f, 0.45f, 0.10f);
    private static readonly Color cRoxo   = new Color(0.65f, 0.25f, 1.00f);
    private static readonly Color cFundo   = new Color(0.13f, 0.13f, 0.18f);

    // ════════════════════════════════════════════════════════
    //  ABRIR
    // ════════════════════════════════════════════════════════
    [MenuItem("Ferramentas/Alinhador & Distribuidor UI  %#a")]
    public static void Abrir()
    {
        var w = GetWindow<AlinhadorUI>("⬛ Alinhador UI");
        w.minSize = new Vector2(320, 490);
        w.maxSize = new Vector2(320, 490);
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
        DesenharInfoSelecao();
        EditorGUILayout.Space(6);

        DesenharGrupoAlinhar();
        EditorGUILayout.Space(8);

        DesenharGrupoDistribuir();
        EditorGUILayout.Space(8);

        DesenharGrupoIgualar();
        EditorGUILayout.Space(8);

        DesenharGrupoNudge();
        EditorGUILayout.Space(8);

        DesenharGrupoEspacamento();

        GUILayout.EndScrollView();
    }

    // ════════════════════════════════════════════════════════
    //  CABEÇALHO
    // ════════════════════════════════════════════════════════
    void DesenharCabecalho()
    {
        EditorGUILayout.Space(6);
        GUILayout.Label("ALINHADOR & DISTRIBUIDOR UI", sTitulo);
        EditorGUILayout.Space(2);
        Rect linha = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(linha, new Color(cAzul.r, cAzul.g, cAzul.b, 0.5f));
        EditorGUILayout.Space(6);
    }

    void DesenharInfoSelecao()
    {
        int qtd = Selection.gameObjects?.Length ?? 0;
        string msg = qtd == 0 ? "⚠ Nenhum objeto selecionado"
                   : qtd == 1 ? $"✦ 1 objeto — selecione 2+ para alinhar/distribuir"
                   :            $"✦ {qtd} objetos selecionados";
        MessageType tipo = qtd >= 2 ? MessageType.Info : MessageType.Warning;
        EditorGUILayout.HelpBox(msg, tipo);
    }

    // ════════════════════════════════════════════════════════
    //  GRUPO — ALINHAR
    // ════════════════════════════════════════════════════════
    void DesenharGrupoAlinhar()
    {
        GUILayout.BeginVertical(sSecao);
        DesenharSubtitulo("▣  ALINHAR", cAzul);
        EditorGUILayout.Space(6);

        // Linha Horizontal
        GUILayout.Label("  Horizontal", sLabel);
        GUILayout.BeginHorizontal();
        BotaoAcao("⬛◁\nEsq",    "Alinhar borda esquerda",  () => Alinhar(AlinhTipo.Esquerda));
        BotaoAcao("◁⬛▷\nCentro H", "Centralizar horizontalmente", () => Alinhar(AlinhTipo.CentroH));
        BotaoAcao("▷⬛\nDir",    "Alinhar borda direita",   () => Alinhar(AlinhTipo.Direita));
        GUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        // Linha Vertical
        GUILayout.Label("  Vertical", sLabel);
        GUILayout.BeginHorizontal();
        BotaoAcao("▲⬛\nTopo",    "Alinhar borda superior",  () => Alinhar(AlinhTipo.Topo));
        BotaoAcao("▲⬛▼\nCentro V", "Centralizar verticalmente", () => Alinhar(AlinhTipo.CentroV));
        BotaoAcao("⬛▼\nBase",    "Alinhar borda inferior",  () => Alinhar(AlinhTipo.Base));
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  GRUPO — DISTRIBUIR
    // ════════════════════════════════════════════════════════
    void DesenharGrupoDistribuir()
    {
        GUILayout.BeginVertical(sSecao);
        DesenharSubtitulo("↔  DISTRIBUIR ESPAÇAMENTO", cVerde);
        EditorGUILayout.Space(6);

        usarGapFixo = EditorGUILayout.Toggle("Usar espaçamento fixo (px):", usarGapFixo);
        if (usarGapFixo)
            gapPx = EditorGUILayout.FloatField("   Gap (px):", gapPx);

        EditorGUILayout.Space(4);
        GUILayout.BeginHorizontal();
        BotaoAcaoVerde("↔ Distribuir\nHorizontal",  "Espaço igual entre elementos (eixo X)", () => Distribuir(false));
        BotaoAcaoVerde("↕ Distribuir\nVertical",    "Espaço igual entre elementos (eixo Y)", () => Distribuir(true));
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  GRUPO — IGUALAR TAMANHO
    // ════════════════════════════════════════════════════════
    void DesenharGrupoIgualar()
    {
        GUILayout.BeginVertical(sSecao);
        DesenharSubtitulo("⬛  IGUALAR TAMANHO", cRoxo);
        EditorGUILayout.Space(6);
        GUILayout.Label("  Usa o primeiro objeto selecionado como referência.", sLabel);
        EditorGUILayout.Space(4);

        GUILayout.BeginHorizontal();
        BotaoAcaoRoxo("↔\nLargura",      "Igualar largura ao primeiro objeto", () => IgualarTamanho(true, false));
        BotaoAcaoRoxo("↕\nAltura",       "Igualar altura ao primeiro objeto",  () => IgualarTamanho(false, true));
        BotaoAcaoRoxo("⬛\nLarg + Alt",  "Igualar largura e altura",           () => IgualarTamanho(true, true));
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  GRUPO — NUDGE PIXEL
    // ════════════════════════════════════════════════════════
    void DesenharGrupoNudge()
    {
        GUILayout.BeginVertical(sSecao);
        DesenharSubtitulo("✥  NUDGE PIXEL-PERFECT", cLaranja);
        EditorGUILayout.Space(4);

        GUILayout.BeginHorizontal();
        GUILayout.Label("Passo (px):", sLabel, GUILayout.Width(80));
        nudgePx = EditorGUILayout.FloatField(nudgePx, GUILayout.Width(60));
        GUILayout.Label("  ← use 0.5 para sub-pixel", EditorStyles.miniLabel);
        GUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        // Cruz de nudge
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("▲", GUILayout.Width(50), GUILayout.Height(32)))
            Nudge(0, nudgePx);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("◀", GUILayout.Width(50), GUILayout.Height(32)))
            Nudge(-nudgePx, 0);
        GUILayout.Space(4);
        if (GUILayout.Button("●", GUILayout.Width(50), GUILayout.Height(32)))
            EditorUtility.DisplayDialog("Nudge", $"Passo atual: {nudgePx}px", "OK");
        GUILayout.Space(4);
        if (GUILayout.Button("▶", GUILayout.Width(50), GUILayout.Height(32)))
            Nudge(nudgePx, 0);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("▼", GUILayout.Width(50), GUILayout.Height(32)))
            Nudge(0, -nudgePx);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  GRUPO — ESPAÇAMENTO ENTRE BORDAS
    // ════════════════════════════════════════════════════════
    void DesenharGrupoEspacamento()
    {
        GUILayout.BeginVertical(sSecao);
        DesenharSubtitulo("↔  ESPAÇO EXATO ENTRE OBJETOS", cAzul);
        EditorGUILayout.Space(4);
        GUILayout.Label("  Define um gap fixo (px) entre os objetos selecionados.", sLabel);
        EditorGUILayout.Space(4);
        gapPx = EditorGUILayout.FloatField("Gap (px):", gapPx);
        EditorGUILayout.Space(4);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("→ Empacotar Horizontal", GUILayout.Height(28)))
            EmpacotarComGap(false);
        if (GUILayout.Button("↓ Empacotar Vertical", GUILayout.Height(28)))
            EmpacotarComGap(true);
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
        EditorGUILayout.Space(6);
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — ALINHAR
    // ════════════════════════════════════════════════════════
    enum AlinhTipo { Esquerda, CentroH, Direita, Topo, CentroV, Base }

    void Alinhar(AlinhTipo tipo)
    {
        var objs = ObjetosValidos();
        if (objs.Count < 2) { Avisar(); return; }

        // Calcula o bounding box geral
        Bounds bounds = BoundsUnificado(objs);

        Undo.RecordObjects(objs.Select(o => (Object)o.transform).ToArray(), "Alinhar Objetos");

        foreach (var go in objs)
        {
            Bounds b = BoundsDeObjeto(go);
            Vector3 pos = go.transform.position;

            switch (tipo)
            {
                case AlinhTipo.Esquerda:
                    pos.x += bounds.min.x - b.min.x; break;
                case AlinhTipo.CentroH:
                    pos.x += bounds.center.x - b.center.x; break;
                case AlinhTipo.Direita:
                    pos.x += bounds.max.x - b.max.x; break;
                case AlinhTipo.Topo:
                    pos.y += bounds.max.y - b.max.y; break;
                case AlinhTipo.CentroV:
                    pos.y += bounds.center.y - b.center.y; break;
                case AlinhTipo.Base:
                    pos.y += bounds.min.y - b.min.y; break;
            }

            AplicarPosicao(go, pos);
        }
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — DISTRIBUIR
    // ════════════════════════════════════════════════════════
    void Distribuir(bool vertical)
    {
        var objs = ObjetosValidos();
        if (objs.Count < 3) { Avisar("Selecione ao menos 3 objetos para distribuir."); return; }

        Undo.RecordObjects(objs.Select(o => (Object)o.transform).ToArray(), "Distribuir Objetos");

        if (!vertical)
        {
            // Ordena por X
            var ordenados = objs.OrderBy(go => go.transform.position.x).ToList();
            float xMin = BoundsDeObjeto(ordenados.First()).min.x;
            float xMax = BoundsDeObjeto(ordenados.Last()).max.x;

            if (usarGapFixo)
            {
                float cursor = xMin;
                foreach (var go in ordenados)
                {
                    Bounds b  = BoundsDeObjeto(go);
                    float meio = b.center.x - b.min.x;
                    Vector3 p = go.transform.position;
                    p.x = cursor + meio;
                    AplicarPosicao(go, p);
                    cursor += b.size.x + gapPx;
                }
            }
            else
            {
                float totalW = ordenados.Sum(go => BoundsDeObjeto(go).size.x);
                float espaco = (xMax - xMin - totalW) / (ordenados.Count - 1);
                float cursor = xMin;
                foreach (var go in ordenados)
                {
                    Bounds b = BoundsDeObjeto(go);
                    Vector3 p = go.transform.position;
                    p.x = cursor + (b.center.x - b.min.x);
                    AplicarPosicao(go, p);
                    cursor += b.size.x + espaco;
                }
            }
        }
        else
        {
            // Ordena por Y (maior = topo)
            var ordenados = objs.OrderByDescending(go => go.transform.position.y).ToList();
            float yMax = BoundsDeObjeto(ordenados.First()).max.y;
            float yMin = BoundsDeObjeto(ordenados.Last()).min.y;

            if (usarGapFixo)
            {
                float cursor = yMax;
                foreach (var go in ordenados)
                {
                    Bounds b   = BoundsDeObjeto(go);
                    float meio = b.max.y - b.center.y;
                    Vector3 p  = go.transform.position;
                    p.y = cursor - meio;
                    AplicarPosicao(go, p);
                    cursor -= b.size.y + gapPx;
                }
            }
            else
            {
                float totalH = ordenados.Sum(go => BoundsDeObjeto(go).size.y);
                float espaco = (yMax - yMin - totalH) / (ordenados.Count - 1);
                float cursor = yMax;
                foreach (var go in ordenados)
                {
                    Bounds b  = BoundsDeObjeto(go);
                    Vector3 p = go.transform.position;
                    p.y = cursor - (b.max.y - b.center.y);
                    AplicarPosicao(go, p);
                    cursor -= b.size.y + espaco;
                }
            }
        }
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — IGUALAR TAMANHO
    // ════════════════════════════════════════════════════════
    void IgualarTamanho(bool largura, bool altura)
    {
        var objs = ObjetosValidos();
        if (objs.Count < 2) { Avisar(); return; }

        GameObject ref0 = objs[0];
        Undo.RecordObjects(objs.Select(o => (Object)o.transform).ToArray(), "Igualar Tamanho");

        RectTransform rtRef = ref0.GetComponent<RectTransform>();

        foreach (var go in objs.Skip(1))
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt != null && rtRef != null)
            {
                Vector2 sz = rt.sizeDelta;
                if (largura) sz.x = rtRef.sizeDelta.x;
                if (altura)  sz.y = rtRef.sizeDelta.y;
                rt.sizeDelta = sz;
            }
            else
            {
                // Para Transform normal: ajusta escala proporcional
                Vector3 escalaRef = ref0.transform.localScale;
                Vector3 escala    = go.transform.localScale;
                if (largura) escala.x = escalaRef.x;
                if (altura)  escala.y = escalaRef.y;
                go.transform.localScale = escala;
            }
        }
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — NUDGE
    // ════════════════════════════════════════════════════════
    void Nudge(float dx, float dy)
    {
        var objs = ObjetosValidos(minimo: 1);
        if (objs.Count == 0) { Avisar("Selecione ao menos 1 objeto."); return; }

        Undo.RecordObjects(objs.Select(o => (Object)o.transform).ToArray(), "Nudge");

        foreach (var go in objs)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt != null)
                rt.anchoredPosition += new Vector2(dx, dy);
            else
                go.transform.position += new Vector3(dx, dy, 0);
        }
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — EMPACOTAR COM GAP FIXO
    // ════════════════════════════════════════════════════════
    void EmpacotarComGap(bool vertical)
    {
        var objs = ObjetosValidos();
        if (objs.Count < 2) { Avisar(); return; }

        Undo.RecordObjects(objs.Select(o => (Object)o.transform).ToArray(), "Empacotar com Gap");

        if (!vertical)
        {
            var ordenados = objs.OrderBy(go => go.transform.position.x).ToList();
            float cursor = BoundsDeObjeto(ordenados[0]).min.x;
            foreach (var go in ordenados)
            {
                Bounds b  = BoundsDeObjeto(go);
                float meio = b.center.x - b.min.x;
                Vector3 p = go.transform.position;
                p.x = cursor + meio;
                AplicarPosicao(go, p);
                cursor += b.size.x + gapPx;
            }
        }
        else
        {
            var ordenados = objs.OrderByDescending(go => go.transform.position.y).ToList();
            float cursor = BoundsDeObjeto(ordenados[0]).max.y;
            foreach (var go in ordenados)
            {
                Bounds b  = BoundsDeObjeto(go);
                float meio = b.max.y - b.center.y;
                Vector3 p  = go.transform.position;
                p.y = cursor - meio;
                AplicarPosicao(go, p);
                cursor -= b.size.y + gapPx;
            }
        }
    }

    // ════════════════════════════════════════════════════════
    //  UTILITÁRIOS DE BOUNDS / POSIÇÃO
    // ════════════════════════════════════════════════════════
    Bounds BoundsDeObjeto(GameObject go)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Bounds b = new Bounds(corners[0], Vector3.zero);
            foreach (var c in corners) b.Encapsulate(c);
            return b;
        }

        // SpriteRenderer
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        if (sr != null) return sr.bounds;

        // Fallback — ponto
        return new Bounds(go.transform.position, Vector3.zero);
    }

    Bounds BoundsUnificado(List<GameObject> objs)
    {
        Bounds b = BoundsDeObjeto(objs[0]);
        foreach (var go in objs.Skip(1))
            b.Encapsulate(BoundsDeObjeto(go));
        return b;
    }

    void AplicarPosicao(GameObject go, Vector3 novaPosicao)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            // Converte de world para anchoredPosition
            Vector3 delta = novaPosicao - go.transform.position;
            rt.anchoredPosition += new Vector2(delta.x, delta.y);
        }
        else
        {
            go.transform.position = novaPosicao;
        }
    }

    List<GameObject> ObjetosValidos(int minimo = 2)
    {
        if (Selection.gameObjects == null) return new List<GameObject>();
        return Selection.gameObjects.Where(go => go != null).ToList();
    }

    void Avisar(string msg = "Selecione ao menos 2 objetos na Hierarchy.")
        => EditorUtility.DisplayDialog("Atenção", msg, "OK");

    // ════════════════════════════════════════════════════════
    //  HELPERS DE BOTÃO
    // ════════════════════════════════════════════════════════
    void BotaoAcao(string label, string tooltip, System.Action acao)
    {
        if (GUILayout.Button(new GUIContent(label, tooltip), sBotao, GUILayout.Height(44)))
            acao();
    }

    void BotaoAcaoVerde(string label, string tooltip, System.Action acao)
    {
        GUI.backgroundColor = new Color(0.15f, 0.65f, 0.35f);
        if (GUILayout.Button(new GUIContent(label, tooltip), sBotao, GUILayout.Height(44)))
            acao();
        GUI.backgroundColor = Color.white;
    }

    void BotaoAcaoRoxo(string label, string tooltip, System.Action acao)
    {
        GUI.backgroundColor = new Color(0.50f, 0.20f, 0.80f);
        if (GUILayout.Button(new GUIContent(label, tooltip), sBotao, GUILayout.Height(44)))
            acao();
        GUI.backgroundColor = Color.white;
    }

    void DesenharSubtitulo(string texto, Color cor)
    {
        var st = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 10,
            normal    = { textColor = cor }
        };
        GUILayout.Label(texto, st);
        Rect r = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(r, new Color(cor.r, cor.g, cor.b, 0.25f));
    }

    // ════════════════════════════════════════════════════════
    //  ESTILOS
    // ════════════════════════════════════════════════════════
    void CarregarEstilos()
    {
        if (estilosOk) return;

        sBotao = new GUIStyle(GUI.skin.button)
        {
            fontSize   = 10,
            fontStyle  = FontStyle.Bold,
            alignment  = TextAnchor.MiddleCenter,
            wordWrap   = true,
            fixedHeight = 0,
        };

        sTitulo = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 13,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(0.5f, 0.85f, 1f) }
        };

        sSecao = new GUIStyle(EditorStyles.helpBox)
        {
            padding = new RectOffset(10, 10, 8, 8)
        };

        sGrupo = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(6, 6, 6, 6)
        };

        sLabel = new GUIStyle(EditorStyles.miniLabel)
        {
            normal = { textColor = new Color(0.7f, 0.7f, 0.8f) }
        };

        estilosOk = true;
    }

    void OnSelectionChange() => Repaint();
}
