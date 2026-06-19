using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using System.Collections.Generic;
using System.Linq;

// ============================================================
// PaletaCoresGlobal.cs  —  Ferramenta de Editor
//
// Abre em: Ferramentas → Paleta de Cores Global   (Ctrl+Shift+P)
//
// Funcionalidades:
//   • Define 5 cores globais do projeto (Primária, Secundária,
//     Destaque, Fundo, Texto)
//   • Aplica às tags: Cor_Primaria / Cor_Secundaria /
//     Cor_Destaque / Cor_Fundo / Cor_Texto
//   • Modo "Aplicar em Tempo Real" — atualiza ao mudar slider
//   • Verificador de contraste WCAG AA/AAA
//   • 6 presets temáticos prontos
//   • Salva / carrega paletas por nome
//   • Compatível com Image (UI) e TextMeshProUGUI
// ============================================================

public class PaletaCoresGlobal : EditorWindow
{
    // ── Cores da paleta ──────────────────────────────────────
    private Color corPrimaria   = new Color(0.18f, 0.52f, 1.00f);
    private Color corSecundaria = new Color(0.12f, 0.78f, 0.55f);
    private Color corDestaque   = new Color(1.00f, 0.38f, 0.18f);
    private Color corFundo      = new Color(0.05f, 0.06f, 0.14f);
    private Color corTexto      = new Color(0.95f, 0.95f, 1.00f);

    // Cores antigas para detectar mudança em tempo real
    private Color oldPrimaria, oldSecundaria, oldDestaque, oldFundo, oldTexto;

    // ── Estado da janela ─────────────────────────────────────
    private int    abaAtiva       = 0;
    private string[] abas         = { "🎨 Paleta", "📊 Contraste", "💾 Presets" };
    private Vector2  scroll       = Vector2.zero;
    private bool     tempoReal    = false;
    private bool     somenteSelec = false;
    private string   nomePaleta   = "Minha Paleta";

    // ── Estilos ──────────────────────────────────────────────
    private bool estilosOk = false;
    private GUIStyle estiloTitulo, estiloSecao, estiloAba, estiloBotaoVerde,
                     estiloBotaoAzul, estiloBotaoLaranja, estiloTag, estiloInfo;

    // ── Tags de mapeamento ───────────────────────────────────
    private const string TAG_PRIMARIA   = "Cor_Primaria";
    private const string TAG_SECUNDARIA = "Cor_Secundaria";
    private const string TAG_DESTAQUE   = "Cor_Destaque";
    private const string TAG_FUNDO      = "Cor_Fundo";
    private const string TAG_TEXTO      = "Cor_Texto";

    // ── Presets temáticos ────────────────────────────────────
    private static readonly Dictionary<string, Color[]> presets = new Dictionary<string, Color[]>
    {
        { "🌌 Alien City",   new[]{ new Color(0.18f,0.52f,1.00f), new Color(0.12f,0.78f,0.55f), new Color(1.00f,0.38f,0.18f), new Color(0.05f,0.06f,0.14f), new Color(0.95f,0.95f,1.00f) } },
        { "🔥 Cyberpunk",    new[]{ new Color(1.00f,0.06f,0.45f), new Color(0.00f,0.90f,1.00f), new Color(1.00f,0.85f,0.00f), new Color(0.04f,0.04f,0.10f), Color.white } },
        { "🌿 Natureza",     new[]{ new Color(0.20f,0.70f,0.30f), new Color(0.60f,0.85f,0.25f), new Color(1.00f,0.75f,0.00f), new Color(0.07f,0.12f,0.07f), new Color(0.95f,1.00f,0.92f) } },
        { "🌊 Oceano",       new[]{ new Color(0.00f,0.60f,0.85f), new Color(0.20f,0.85f,0.90f), new Color(0.00f,1.00f,0.75f), new Color(0.02f,0.08f,0.18f), Color.white } },
        { "🌸 Candy",        new[]{ new Color(1.00f,0.40f,0.65f), new Color(0.85f,0.55f,1.00f), new Color(1.00f,0.85f,0.35f), new Color(0.12f,0.05f,0.18f), Color.white } },
        { "🌑 Monocromático",new[]{ new Color(0.75f,0.75f,0.75f), new Color(0.50f,0.50f,0.50f), Color.white,                  new Color(0.05f,0.05f,0.05f), Color.white } },
    };

    // ════════════════════════════════════════════════════════
    //  ABRIR JANELA
    // ════════════════════════════════════════════════════════
    [MenuItem("Ferramentas/Paleta de Cores Global  %#p")]
    public static void Abrir()
    {
        var w = GetWindow<PaletaCoresGlobal>("🎨 Paleta Global");
        w.minSize = new Vector2(340, 580);
        w.CarregarPrefs();
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
        abaAtiva = GUILayout.Toolbar(abaAtiva, abas, GUILayout.Height(30));
        EditorGUILayout.Space(8);

        switch (abaAtiva)
        {
            case 0: AbraPaleta();   break;
            case 1: AbraContraste(); break;
            case 2: AbraPresets();  break;
        }

        GUILayout.EndScrollView();

        // Aplica em tempo real ao detectar mudança de cor
        if (tempoReal && CoresAlteraram())
        {
            AplicarNaCena(silencioso: true);
            SalvarCoresAntigas();
        }
    }

    // ════════════════════════════════════════════════════════
    //  ABA PALETA
    // ════════════════════════════════════════════════════════
    void AbraPaleta()
    {
        // ── Preview visual das cores ──────────────────────
        GUILayout.BeginVertical(estiloSecao);
        GUILayout.Label("PRÉVIA DA PALETA", estiloTitulo);
        EditorGUILayout.Space(6);
        DesenharPreview();
        GUILayout.EndVertical();
        EditorGUILayout.Space(8);

        // ── Editores de cor ───────────────────────────────
        GUILayout.BeginVertical(estiloSecao);
        GUILayout.Label("CORES DO PROJETO", estiloTitulo);
        EditorGUILayout.Space(6);

        DesenharLinhaCor("● Primária",   TAG_PRIMARIA,   ref corPrimaria);
        DesenharLinhaCor("● Secundária", TAG_SECUNDARIA, ref corSecundaria);
        DesenharLinhaCor("● Destaque",   TAG_DESTAQUE,   ref corDestaque);
        DesenharLinhaCor("● Fundo",      TAG_FUNDO,      ref corFundo);
        DesenharLinhaCor("● Texto",      TAG_TEXTO,      ref corTexto);

        GUILayout.EndVertical();
        EditorGUILayout.Space(8);

        // ── Opções de aplicação ───────────────────────────
        GUILayout.BeginVertical(estiloSecao);
        GUILayout.Label("OPÇÕES DE APLICAÇÃO", estiloTitulo);
        EditorGUILayout.Space(4);

        tempoReal    = EditorGUILayout.Toggle("⚡ Aplicar em Tempo Real", tempoReal);
        somenteSelec = EditorGUILayout.Toggle("🎯 Só nos Selecionados", somenteSelec);
        EditorGUILayout.Space(4);

        int cont = ContarAlvos();
        EditorGUILayout.HelpBox(
            somenteSelec
                ? $"{Selection.gameObjects?.Length ?? 0} objeto(s) selecionado(s)"
                : $"{cont} componente(s) com tags de cor na cena",
            MessageType.Info);
        GUILayout.EndVertical();
        EditorGUILayout.Space(8);

        // ── Botões de ação ────────────────────────────────
        GUILayout.BeginVertical(estiloSecao);

        if (GUILayout.Button("✅  APLICAR NA CENA", estiloBotaoVerde))
        {
            AplicarNaCena(silencioso: false);
            SalvarPrefs();
        }

        EditorGUILayout.Space(4);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("↩  Desfazer", estiloBotaoAzul))
            Undo.PerformUndo();
        if (GUILayout.Button("💾 Salvar Prefs", estiloBotaoAzul))
            SalvarPrefs();
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
        EditorGUILayout.Space(6);

        DesenharInstrucoes();
    }

    // ════════════════════════════════════════════════════════
    //  ABA CONTRASTE WCAG
    // ════════════════════════════════════════════════════════
    void AbraContraste()
    {
        GUILayout.BeginVertical(estiloSecao);
        GUILayout.Label("VERIFICADOR DE CONTRASTE WCAG", estiloTitulo);
        EditorGUILayout.Space(4);
        GUILayout.Label("Analisa a legibilidade e acessibilidade visual\nda sua paleta (padrão WCAG 2.1).", EditorStyles.wordWrappedMiniLabel);
        GUILayout.EndVertical();
        EditorGUILayout.Space(8);

        // Pares importantes de verificar
        DesenharContraste("Texto sobre Fundo",      corTexto,      corFundo);
        DesenharContraste("Primária sobre Fundo",   corPrimaria,   corFundo);
        DesenharContraste("Destaque sobre Fundo",   corDestaque,   corFundo);
        DesenharContraste("Secundária sobre Fundo", corSecundaria, corFundo);
        DesenharContraste("Texto sobre Primária",   corTexto,      corPrimaria);
        DesenharContraste("Texto sobre Destaque",   corTexto,      corDestaque);

        EditorGUILayout.Space(8);
        GUILayout.BeginVertical(estiloSecao);
        GUILayout.Label("LEGENDA", estiloTitulo);
        EditorGUILayout.Space(4);
        GUILayout.Label("✅ AAA  — Contraste ≥ 7:1  (excelente acessibilidade)", EditorStyles.miniLabel);
        GUILayout.Label("🟡 AA   — Contraste ≥ 4.5:1 (bom para texto normal)",   EditorStyles.miniLabel);
        GUILayout.Label("🟠 AA+  — Contraste ≥ 3:1   (mínimo para texto grande)", EditorStyles.miniLabel);
        GUILayout.Label("❌ Falha — Contraste < 3:1   (inacessível)",             EditorStyles.miniLabel);
        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  ABA PRESETS
    // ════════════════════════════════════════════════════════
    void AbraPresets()
    {
        GUILayout.BeginVertical(estiloSecao);
        GUILayout.Label("TEMAS PRONTOS", estiloTitulo);
        EditorGUILayout.Space(6);

        foreach (var preset in presets)
        {
            GUILayout.BeginHorizontal();

            // Mini preview das cores do preset
            Rect r = GUILayoutUtility.GetRect(110, 22);
            float larg = r.width / 5f;
            for (int i = 0; i < 5; i++)
                EditorGUI.DrawRect(new Rect(r.x + i * larg, r.y, larg, r.height), preset.Value[i]);

            if (GUILayout.Button(preset.Key, GUILayout.Height(22)))
            {
                corPrimaria   = preset.Value[0];
                corSecundaria = preset.Value[1];
                corDestaque   = preset.Value[2];
                corFundo      = preset.Value[3];
                corTexto      = preset.Value[4];

                if (tempoReal) AplicarNaCena(silencioso: true);
                abaAtiva = 0; // volta para aba paleta
            }

            GUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }

        GUILayout.EndVertical();
        EditorGUILayout.Space(8);

        // ── Salvar preset personalizado ───────────────────
        GUILayout.BeginVertical(estiloSecao);
        GUILayout.Label("SALVAR PALETA ATUAL", estiloTitulo);
        EditorGUILayout.Space(4);
        nomePaleta = EditorGUILayout.TextField("Nome:", nomePaleta);
        EditorGUILayout.Space(4);
        if (GUILayout.Button("💾  Salvar nas Prefs do Editor", estiloBotaoAzul))
        {
            SalvarPrefs();
            EditorUtility.DisplayDialog("Salvo!", $"Paleta \"{nomePaleta}\" salva com sucesso!", "OK");
        }
        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — APLICAR NA CENA
    // ════════════════════════════════════════════════════════
    void AplicarNaCena(bool silencioso)
    {
        int total = 0;

        if (somenteSelec)
            total = AplicarNosSelecionados();
        else
            total = AplicarPorTag();

        if (!silencioso)
        {
            EditorUtility.DisplayDialog(
                "Paleta Aplicada! ✅",
                $"{total} componente(s) atualizado(s) na cena.\n\nUse Ctrl+Z para desfazer.",
                "OK");
        }

        Debug.Log($"[PaletaCores] {total} componente(s) atualizados.");
    }

    int AplicarPorTag()
    {
        int total = 0;
        var mapa  = MapaTagCor();

        // Image (UI)
        foreach (Image img in FindObjectsOfType<Image>())
        {
            if (img == null) continue;
            foreach (var par in mapa)
            {
                if (img.gameObject.CompareTag(par.Key))
                {
                    Undo.RecordObject(img, "Aplicar Paleta");
                    img.color = par.Value;
                    total++;
                    break;
                }
            }
        }

        // TextMeshProUGUI
        foreach (TextMeshProUGUI tmp in FindObjectsOfType<TextMeshProUGUI>())
        {
            if (tmp == null) continue;
            foreach (var par in mapa)
            {
                if (tmp.gameObject.CompareTag(par.Key))
                {
                    Undo.RecordObject(tmp, "Aplicar Paleta");
                    tmp.color = par.Value;
                    total++;
                    break;
                }
            }
        }

        // SpriteRenderer (fora do Canvas)
        foreach (SpriteRenderer sr in FindObjectsOfType<SpriteRenderer>())
        {
            if (sr == null) continue;
            foreach (var par in mapa)
            {
                if (sr.gameObject.CompareTag(par.Key))
                {
                    Undo.RecordObject(sr, "Aplicar Paleta");
                    sr.color = par.Value;
                    total++;
                    break;
                }
            }
        }

        return total;
    }

    int AplicarNosSelecionados()
    {
        int total = 0;
        GameObject[] selecionados = Selection.gameObjects;
        if (selecionados == null) return 0;

        var mapa = MapaTagCor();

        foreach (GameObject go in selecionados)
        {
            Color cor = corPrimaria; // padrão se sem tag
            bool temTag = false;
            foreach (var par in mapa)
            {
                if (go.CompareTag(par.Key)) { cor = par.Value; temTag = true; break; }
            }

            // Aplica nos componentes do objeto
            Image img = go.GetComponent<Image>();
            if (img != null) { Undo.RecordObject(img, "Aplicar Paleta"); img.color = cor; total++; }

            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            if (tmp != null) { Undo.RecordObject(tmp, "Aplicar Paleta"); tmp.color = cor; total++; }

            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            if (sr != null) { Undo.RecordObject(sr, "Aplicar Paleta"); sr.color = cor; total++; }
        }

        return total;
    }

    Dictionary<string, Color> MapaTagCor() => new Dictionary<string, Color>
    {
        { TAG_PRIMARIA,   corPrimaria   },
        { TAG_SECUNDARIA, corSecundaria },
        { TAG_DESTAQUE,   corDestaque   },
        { TAG_FUNDO,      corFundo      },
        { TAG_TEXTO,      corTexto      },
    };

    int ContarAlvos()
    {
        var mapa = MapaTagCor();
        int c = 0;
        foreach (Image      img in FindObjectsOfType<Image>())           if (mapa.ContainsKey(img.tag)) c++;
        foreach (TextMeshProUGUI t in FindObjectsOfType<TextMeshProUGUI>()) if (mapa.ContainsKey(t.tag))  c++;
        foreach (SpriteRenderer sr in FindObjectsOfType<SpriteRenderer>())  if (mapa.ContainsKey(sr.tag)) c++;
        return c;
    }

    // ════════════════════════════════════════════════════════
    //  UI — PREVIEW VISUAL DAS CORES
    // ════════════════════════════════════════════════════════
    void DesenharPreview()
    {
        float h = 40f;
        Rect total = GUILayoutUtility.GetRect(0, h + 24, GUILayout.ExpandWidth(true));

        string[] labels = { "Primária", "Secundária", "Destaque", "Fundo", "Texto" };
        Color[]  cores  = { corPrimaria, corSecundaria, corDestaque, corFundo, corTexto };
        float larg = total.width / 5f;

        for (int i = 0; i < 5; i++)
        {
            Rect bloco = new Rect(total.x + i * larg, total.y, larg - 2, h);
            EditorGUI.DrawRect(bloco, cores[i]);

            // Label abaixo
            Rect lblRect = new Rect(bloco.x, bloco.y + h + 2, larg, 16);
            GUI.Label(lblRect, labels[i], new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                fontSize = 8, alignment = TextAnchor.MiddleCenter
            });
        }
        GUILayout.Space(h + 20);
    }

    // ── Linha de cor com tag info ─────────────────────────
    void DesenharLinhaCor(string label, string tag, ref Color cor)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(90));
        cor = EditorGUILayout.ColorField(GUIContent.none, cor, true, true, false, GUILayout.Width(60));
        GUILayout.Label($"tag: ", EditorStyles.miniLabel, GUILayout.Width(28));
        GUILayout.Label(tag, estiloTag);
        GUILayout.EndHorizontal();
        EditorGUILayout.Space(2);
    }

    // ── Card de contraste WCAG ────────────────────────────
    void DesenharContraste(string descricao, Color fg, Color bg)
    {
        float ratio = ContrastRatio(fg, bg);
        string nivel, icone;
        MessageType tipo;

        if      (ratio >= 7f)   { nivel = "AAA";   icone = "✅"; tipo = MessageType.None; }
        else if (ratio >= 4.5f) { nivel = "AA";    icone = "🟡"; tipo = MessageType.Warning; }
        else if (ratio >= 3f)   { nivel = "AA+";   icone = "🟠"; tipo = MessageType.Warning; }
        else                    { nivel = "Falha";  icone = "❌"; tipo = MessageType.Error; }

        GUILayout.BeginVertical(estiloSecao);
        GUILayout.BeginHorizontal();

        // Mini preview
        Rect prev = GUILayoutUtility.GetRect(60, 28, GUILayout.Width(60));
        EditorGUI.DrawRect(prev, bg);
        Rect innerPrev = new Rect(prev.x + 4, prev.y + 4, prev.width - 8, prev.height - 8);
        EditorGUI.DrawRect(innerPrev, fg);

        GUILayout.BeginVertical();
        GUILayout.Label(descricao, EditorStyles.boldLabel);
        GUILayout.Label($"{icone} {nivel}  —  {ratio:F2}:1", EditorStyles.miniLabel);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
        EditorGUILayout.Space(4);
    }

    // ── Instruções de como usar tags ─────────────────────
    void DesenharInstrucoes()
    {
        GUILayout.BeginVertical(estiloInfo);
        GUILayout.Label("📌 COMO USAR AS TAGS", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);
        GUILayout.Label("Selecione um objeto na Hierarchy e atribua\numa das tags abaixo no campo Tag do Inspector:", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space(4);
        GUILayout.Label($"  Cor_Primaria   → cor primária",   EditorStyles.miniLabel);
        GUILayout.Label($"  Cor_Secundaria → cor secundária", EditorStyles.miniLabel);
        GUILayout.Label($"  Cor_Destaque   → cor de destaque", EditorStyles.miniLabel);
        GUILayout.Label($"  Cor_Fundo      → cor de fundo",   EditorStyles.miniLabel);
        GUILayout.Label($"  Cor_Texto      → cor de texto",   EditorStyles.miniLabel);
        GUILayout.EndVertical();
        EditorGUILayout.Space(4);
    }

    // ════════════════════════════════════════════════════════
    //  MATH — CONTRASTE WCAG 2.1
    // ════════════════════════════════════════════════════════
    float ContrastRatio(Color c1, Color c2)
    {
        float L1 = LuminanciaRelativa(c1);
        float L2 = LuminanciaRelativa(c2);
        float bright = Mathf.Max(L1, L2);
        float dark   = Mathf.Min(L1, L2);
        return (bright + 0.05f) / (dark + 0.05f);
    }

    float LuminanciaRelativa(Color c)
    {
        float r = Canal(c.r), g = Canal(c.g), b = Canal(c.b);
        return 0.2126f * r + 0.7152f * g + 0.0722f * b;
    }

    float Canal(float v)
        => v <= 0.03928f ? v / 12.92f : Mathf.Pow((v + 0.055f) / 1.055f, 2.4f);

    // ════════════════════════════════════════════════════════
    //  PERSISTÊNCIA — EditorPrefs
    // ════════════════════════════════════════════════════════
    void SalvarPrefs()
    {
        EditorPrefs.SetString("PG_primaria",   ColorUtility.ToHtmlStringRGBA(corPrimaria));
        EditorPrefs.SetString("PG_secundaria", ColorUtility.ToHtmlStringRGBA(corSecundaria));
        EditorPrefs.SetString("PG_destaque",   ColorUtility.ToHtmlStringRGBA(corDestaque));
        EditorPrefs.SetString("PG_fundo",      ColorUtility.ToHtmlStringRGBA(corFundo));
        EditorPrefs.SetString("PG_texto",      ColorUtility.ToHtmlStringRGBA(corTexto));
        EditorPrefs.SetString("PG_nome",       nomePaleta);
    }

    void CarregarPrefs()
    {
        TryParseColor(EditorPrefs.GetString("PG_primaria",   ""), ref corPrimaria);
        TryParseColor(EditorPrefs.GetString("PG_secundaria", ""), ref corSecundaria);
        TryParseColor(EditorPrefs.GetString("PG_destaque",   ""), ref corDestaque);
        TryParseColor(EditorPrefs.GetString("PG_fundo",      ""), ref corFundo);
        TryParseColor(EditorPrefs.GetString("PG_texto",      ""), ref corTexto);
        nomePaleta = EditorPrefs.GetString("PG_nome", "Minha Paleta");
        SalvarCoresAntigas();
    }

    void TryParseColor(string hex, ref Color dest)
    {
        if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString("#" + hex, out Color c))
            dest = c;
    }

    // ── Detecção de mudança em tempo real ────────────────
    bool CoresAlteraram() =>
        corPrimaria   != oldPrimaria   ||
        corSecundaria != oldSecundaria ||
        corDestaque   != oldDestaque   ||
        corFundo      != oldFundo      ||
        corTexto      != oldTexto;

    void SalvarCoresAntigas()
    {
        oldPrimaria   = corPrimaria;
        oldSecundaria = corSecundaria;
        oldDestaque   = corDestaque;
        oldFundo      = corFundo;
        oldTexto      = corTexto;
    }

    // ════════════════════════════════════════════════════════
    //  ESTILOS
    // ════════════════════════════════════════════════════════
    void CarregarEstilos()
    {
        if (estilosOk) return;

        estiloTitulo = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 10,
            alignment = TextAnchor.MiddleCenter,
        };
        estiloTitulo.normal.textColor = new Color(0.6f, 0.9f, 1f);

        estiloSecao = new GUIStyle(EditorStyles.helpBox)
        {
            padding = new RectOffset(10, 10, 8, 8)
        };

        estiloInfo = new GUIStyle(EditorStyles.helpBox)
        {
            padding = new RectOffset(10, 10, 8, 8)
        };

        estiloTag = new GUIStyle(EditorStyles.miniLabel)
        {
            fontStyle  = FontStyle.Bold,
            normal     = { textColor = new Color(0.4f, 1f, 0.7f) }
        };

        estiloBotaoVerde = new GUIStyle(GUI.skin.button)
        {
            fontSize    = 12,
            fixedHeight = 38,
            fontStyle   = FontStyle.Bold,
            normal      = { textColor = Color.white }
        };

        estiloBotaoAzul = new GUIStyle(GUI.skin.button)
        {
            fontSize    = 11,
            fixedHeight = 30,
        };

        estiloBotaoLaranja = new GUIStyle(GUI.skin.button)
        {
            fontSize    = 11,
            fixedHeight = 30,
        };

        estilosOk = true;
    }

    void DesenharCabecalho()
    {
        EditorGUILayout.Space(6);
        var st = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 14,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(0.5f, 0.85f, 1f) }
        };
        GUILayout.Label("🎨  PALETA DE CORES GLOBAL", st);
        EditorGUILayout.Space(2);
        Rect linha = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(linha, new Color(0.4f, 0.85f, 1f, 0.35f));
        EditorGUILayout.Space(6);
    }

    void OnSelectionChange() => Repaint();
    void OnInspectorUpdate()  { if (tempoReal) Repaint(); }
}
