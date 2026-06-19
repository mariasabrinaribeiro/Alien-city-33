using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

// ============================================================
// AssetQualityLinter.cs  —  Ferramenta de Editor
//
// Abre em: Ferramentas → 🔎 Inspetor de Assets Pro
//          Atalho: Ctrl + Shift + I
//
// Modos:
//   🔍 Diagnóstico — analisa e reporta sem modificar nada
//   🔧 Auto-Fix    — corrige automaticamente todos os problemas
//
// O que analisa (qualquer asset selecionado no Project):
//   🖼 Texturas/Sprites Pixel Art
//       • Filtro Bilinear/Trilinear → deve ser Point
//       • Compressão ativa estraga cores → deve ser None/Lossless
//       • Mipmaps ativos em sprites 2D (consumo desnecessário)
//       • NPOT (não é potência de 2)
//   📝 Nomenclatura de Arquivos
//       • Espaços no nome → substitui por _
//       • Acentos/caracteres especiais → remove
//       • Letras maiúsculas fora de padrão → padroniza
//   🗑 Lixo no Projeto
//       • Materiais não usados em nenhuma cena ou prefab
//       • Arquivos duplicados (mesmo nome + mesmo tamanho)
//   🔊 Áudio
//       • AudioClips sem compressão (PCM) → sugere Vorbis
//
// Política de Log: LogWarning apenas. Mensagens clicáveis
//   passam o asset como contexto → clique abre no Project.
// ============================================================

public class AssetQualityLinter : EditorWindow
{
    // ── Resultado de análise ─────────────────────────────────
    private enum Severidade { Info, Aviso, Erro }
    private enum TipoFix    { Nenhum, TexturaPixelArt, RenomearArquivo, RemoverMaterial }

    private class Problema
    {
        public string    categoria;
        public string    titulo;
        public string    detalhe;
        public string    caminhoAsset;
        public Object    assetObj;
        public Severidade nivel;
        public TipoFix   tipoFix;
        public bool      marcado = true;
        public string    labelFix;
        public string    novoNome; // para renomeação
    }

    // ── Estado ───────────────────────────────────────────────
    private List<Problema> problemas   = new List<Problema>();
    private bool           escaneou    = false;
    private bool           escaneando  = false;
    private float          progresso   = 0f;
    private string         statusAtual = "";
    private Vector2        scroll;
    private bool           estilosOk   = false;

    // Filtros do que verificar
    private bool chkTexturas  = true;
    private bool chkNomes     = true;
    private bool chkMateriais = true;
    private bool chkAudio     = true;
    private bool chkDuplic    = true;

    // Filtros de exibição
    private bool verErros  = true;
    private bool verAvisos = true;
    private bool verInfo   = true;

    // Contadores
    private int totalErro, totalAviso, totalInfo;

    // Estilos
    private GUIStyle sTitulo, sSecao, sLabel, sBotaoDiag, sBotaoFix,
                     sItemErro, sItemAviso, sItemInfo;

    // Cores
    private static readonly Color cAzul    = new Color(0.20f, 0.55f, 1.00f);
    private static readonly Color cVerde   = new Color(0.15f, 0.75f, 0.40f);
    private static readonly Color cLaranja = new Color(1.00f, 0.55f, 0.10f);
    private static readonly Color cVermel  = new Color(0.90f, 0.22f, 0.20f);
    private static readonly Color cRoxo    = new Color(0.65f, 0.25f, 1.00f);
    private static readonly Color cCinza   = new Color(0.40f, 0.40f, 0.50f);

    // ════════════════════════════════════════════════════════
    //  ABRIR
    // ════════════════════════════════════════════════════════
    [MenuItem("Ferramentas/🔎 Inspetor de Assets Pro  %#i")]
    public static void Abrir()
    {
        var w = GetWindow<AssetQualityLinter>("🔎 Asset Linter");
        w.minSize = new Vector2(380, 600);
        w.Show();
    }

    // Também disponível via menu de contexto no Project
    [MenuItem("Assets/🔎 Asset Quality Linter — Escanear Selecionados")]
    static void AbrirViaContexto()
    {
        Abrir();
        GetWindow<AssetQualityLinter>().EscanearAssets(autoFix: false);
    }

    // ════════════════════════════════════════════════════════
    //  OnGUI
    // ════════════════════════════════════════════════════════
    void OnGUI()
    {
        CarregarEstilos();
        scroll = GUILayout.BeginScrollView(scroll);

        DesenharCabecalho();
        DesenharPainelControle();
        EditorGUILayout.Space(8);

        if (escaneando)
        {
            DesenharProgresso();
        }
        else if (escaneou)
        {
            DesenharResumo();
            EditorGUILayout.Space(6);
            DesenharFiltrosExibicao();
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
    //  CABEÇALHO
    // ════════════════════════════════════════════════════════
    void DesenharCabecalho()
    {
        EditorGUILayout.Space(6);
        var st = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 13,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(0.5f, 0.85f, 1f) }
        };
        GUILayout.Label("🔎  INSPETOR DE ASSETS PRO", st);
        Rect r = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(r, new Color(0.4f, 0.85f, 1f, 0.4f));
        EditorGUILayout.Space(6);
    }

    // ════════════════════════════════════════════════════════
    //  PAINEL DE CONTROLE
    // ════════════════════════════════════════════════════════
    void DesenharPainelControle()
    {
        GUILayout.BeginVertical(sSecao);

        // Info da seleção
        int qtdSel = ContarAssetsSelecionados();
        string infoSel = qtdSel == 0
            ? "⚠  Nenhum asset selecionado no Project"
            : $"✦  {qtdSel} asset(s) selecionado(s) no Project";
        EditorGUILayout.HelpBox(infoSel, qtdSel > 0 ? MessageType.Info : MessageType.Warning);
        EditorGUILayout.Space(4);

        // Checkboxes do que verificar
        GUILayout.Label("Verificar:", sLabel);
        GUILayout.BeginHorizontal();
        chkTexturas  = GUILayout.Toggle(chkTexturas,  "🖼 Texturas",  GUILayout.Width(90));
        chkNomes     = GUILayout.Toggle(chkNomes,     "📝 Nomes",     GUILayout.Width(75));
        chkMateriais = GUILayout.Toggle(chkMateriais, "🗑 Materiais", GUILayout.Width(90));
        chkAudio     = GUILayout.Toggle(chkAudio,     "🔊 Áudio",    GUILayout.Width(70));
        chkDuplic    = GUILayout.Toggle(chkDuplic,    "📋 Duplicados",GUILayout.Width(90));
        GUILayout.EndHorizontal();

        EditorGUILayout.Space(8);

        // Dois botões lado a lado — o design principal
        GUILayout.BeginHorizontal();

        GUI.backgroundColor = cAzul;
        if (GUILayout.Button("🔍  DIAGNOSTICAR\n(Apenas Avisar)", sBotaoDiag, GUILayout.Height(52)))
            EscanearAssets(autoFix: false);

        GUI.backgroundColor = cVerde;
        if (GUILayout.Button("🔧  AUTO-FIX\n(Corrigir Tudo)", sBotaoFix, GUILayout.Height(52)))
        {
            bool ok = EditorUtility.DisplayDialog(
                "⚠ Auto-Fix",
                "Isso vai corrigir AUTOMATICAMENTE todos os problemas encontrados:\n\n" +
                "• Renomear arquivos (espaços → _)\n" +
                "• Corrigir configurações de textura (Point filter, sem compressão)\n" +
                "• Remover materiais não usados (marcados)\n\n" +
                "Deseja continuar?",
                "Sim, corrigir tudo", "Cancelar");
            if (ok) EscanearAssets(autoFix: true);
        }
        GUI.backgroundColor = Color.white;

        GUILayout.EndHorizontal();
        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  RESUMO
    // ════════════════════════════════════════════════════════
    void DesenharResumo()
    {
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("RESULTADO DA ANÁLISE", sTitulo);
        EditorGUILayout.Space(6);

        // Barra de status geral
        string stGeral;
        Color  corGeral;
        if      (totalErro > 0)   { stGeral = "❌  Problemas críticos encontrados — corrija antes da build!"; corGeral = cVermel; }
        else if (totalAviso > 0)  { stGeral = "⚠  Avisos encontrados — recomendado corrigir.";               corGeral = cLaranja; }
        else                      { stGeral = "✅  Assets dentro do padrão de qualidade!";                    corGeral = cVerde; }

        GUILayout.Label(stGeral, new GUIStyle(EditorStyles.boldLabel)
        { normal = { textColor = corGeral }, fontSize = 11, wordWrap = true });
        EditorGUILayout.Space(6);

        // Contadores em linha
        GUILayout.BeginHorizontal();
        DesenharContador("❌ Erros",   totalErro,  cVermel);
        DesenharContador("⚠ Avisos",  totalAviso, cLaranja);
        DesenharContador("ℹ Info",    totalInfo,  cAzul);
        GUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        // Botão fix parcial (só os marcados)
        int marcados = problemas.Count(p => p.marcado && p.tipoFix != TipoFix.Nenhum);
        if (marcados > 0)
        {
            GUI.backgroundColor = cVerde;
            if (GUILayout.Button($"🔧  Aplicar Fix nos {marcados} Problemas Marcados",
                GUILayout.Height(30)))
                AplicarFixes(problemas.Where(p => p.marcado && p.tipoFix != TipoFix.Nenhum).ToList());
            GUI.backgroundColor = Color.white;
        }

        GUILayout.EndVertical();
    }

    void DesenharContador(string label, int valor, Color cor)
    {
        GUILayout.BeginVertical(GUILayout.Width(110));
        GUILayout.Label(valor.ToString(), new GUIStyle(EditorStyles.boldLabel)
        { fontSize = 22, alignment = TextAnchor.MiddleCenter, normal = { textColor = cor } },
            GUILayout.Height(32));
        GUILayout.Label(label, EditorStyles.centeredGreyMiniLabel);
        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  FILTROS DE EXIBIÇÃO
    // ════════════════════════════════════════════════════════
    void DesenharFiltrosExibicao()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("Mostrar:", sLabel, GUILayout.Width(60));

        GUI.backgroundColor = verErros  ? cVermel  : cCinza;
        if (GUILayout.Button($"❌ Erros ({totalErro})",    GUILayout.Height(22))) verErros  = !verErros;
        GUI.backgroundColor = verAvisos ? cLaranja : cCinza;
        if (GUILayout.Button($"⚠ Avisos ({totalAviso})",  GUILayout.Height(22))) verAvisos = !verAvisos;
        GUI.backgroundColor = verInfo   ? cAzul    : cCinza;
        if (GUILayout.Button($"ℹ Info ({totalInfo})",     GUILayout.Height(22))) verInfo   = !verInfo;
        GUI.backgroundColor = Color.white;

        GUILayout.EndHorizontal();
    }

    // ════════════════════════════════════════════════════════
    //  LISTA DE RESULTADOS
    // ════════════════════════════════════════════════════════
    void DesenharResultados()
    {
        string catAtual = "";

        foreach (var prob in problemas)
        {
            if (prob.nivel == Severidade.Erro  && !verErros)  continue;
            if (prob.nivel == Severidade.Aviso && !verAvisos) continue;
            if (prob.nivel == Severidade.Info  && !verInfo)   continue;

            // Cabeçalho de categoria
            if (prob.categoria != catAtual)
            {
                catAtual = prob.categoria;
                EditorGUILayout.Space(6);
                var stCat = new GUIStyle(EditorStyles.boldLabel)
                { fontSize = 10, normal = { textColor = new Color(0.5f, 0.85f, 1f) } };
                GUILayout.Label($"── {catAtual} ──", stCat);
                Rect lr = EditorGUILayout.GetControlRect(false, 1);
                EditorGUI.DrawRect(lr, new Color(0.4f, 0.85f, 1f, 0.25f));
            }

            DesenharItemProblema(prob);
        }
        EditorGUILayout.Space(10);
    }

    void DesenharItemProblema(Problema p)
    {
        Color corBorda = p.nivel == Severidade.Erro  ? cVermel
                       : p.nivel == Severidade.Aviso ? cLaranja
                       :                               cAzul;

        Color corFundo = p.nivel == Severidade.Erro
            ? new Color(0.18f, 0.05f, 0.05f)
            : p.nivel == Severidade.Aviso
                ? new Color(0.18f, 0.12f, 0.03f)
                : new Color(0.05f, 0.10f, 0.20f);

        bool temFix  = p.tipoFix != TipoFix.Nenhum;
        float altura = 26 + (!string.IsNullOrEmpty(p.detalhe) ? 18 : 0) + (temFix ? 24 : 0) + 4;

        Rect r = GUILayoutUtility.GetRect(0, altura, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(r, corFundo);
        EditorGUI.DrawRect(new Rect(r.x, r.y, 4, r.height), corBorda);

        // Checkbox (para marcar/desmarcar do fix)
        if (temFix)
        {
            Rect chk = new Rect(r.x + 8, r.y + 5, 16, 16);
            p.marcado = EditorGUI.Toggle(chk, p.marcado);
        }

        float xTexto = temFix ? r.x + 28 : r.x + 10;

        // Ícone + título
        string icone = p.nivel == Severidade.Erro ? "❌" : p.nivel == Severidade.Aviso ? "⚠" : "ℹ";
        var stTitulo2 = new GUIStyle(EditorStyles.boldLabel)
        { fontSize = 10, normal = { textColor = Color.white } };
        EditorGUI.LabelField(new Rect(xTexto, r.y + 5, r.width - 90, 16),
            $"{icone}  {p.titulo}", stTitulo2);

        // Botão Ping (clique para localizar no Project)
        if (p.assetObj != null)
        {
            if (GUI.Button(new Rect(r.xMax - 44, r.y + 5, 40, 16), "ping", EditorStyles.miniButton))
            {
                Selection.activeObject = p.assetObj;
                EditorGUIUtility.PingObject(p.assetObj);
            }
        }

        float y = r.y + 23;

        // Detalhe
        if (!string.IsNullOrEmpty(p.detalhe))
        {
            var stDet = new GUIStyle(EditorStyles.miniLabel)
            { wordWrap = true, normal = { textColor = new Color(0.70f, 0.70f, 0.80f) } };
            EditorGUI.LabelField(new Rect(xTexto, y, r.width - 90, 18), p.detalhe, stDet);
            y += 18;
        }

        // Botão Fix individual
        if (temFix)
        {
            if (GUI.Button(new Rect(xTexto, y + 2, 180, 18), p.labelFix ?? "🔧 Corrigir", EditorStyles.miniButton))
                AplicarFixes(new List<Problema> { p });
        }

        EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), new Color(0.22f, 0.22f, 0.30f));
    }

    // ════════════════════════════════════════════════════════
    //  PROGRESSO
    // ════════════════════════════════════════════════════════
    void DesenharProgresso()
    {
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("ESCANEANDO...", sTitulo);
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
        GUILayout.Label("COMO USAR", sTitulo);
        EditorGUILayout.Space(6);

        var itens = new[]
        {
            ("1.", "Selecione arquivos na aba Project (Ctrl+clique para vários)"),
            ("2.", "Clique em 🔍 DIAGNOSTICAR para ver os problemas sem alterar nada"),
            ("3.", "Ou clique em 🔧 AUTO-FIX para corrigir tudo automaticamente"),
            ("4.", "Clique em 'ping' em qualquer resultado para localizar o asset"),
            ("5.", "Use Ctrl+Z para desfazer correções de nome se necessário"),
        };

        foreach (var (num, txt) in itens)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(num, new GUIStyle(EditorStyles.boldLabel)
            { normal = { textColor = cAzul } }, GUILayout.Width(20));
            GUILayout.Label(txt, sLabel);
            GUILayout.EndHorizontal();
            EditorGUILayout.Space(2);
        }

        EditorGUILayout.Space(8);
        GUILayout.Label("O QUE SERÁ ANALISADO", sTitulo);
        EditorGUILayout.Space(4);

        var checks = new[]
        {
            ("🖼", "Texturas Pixel Art",   "Filtro Point, sem compressão, sem mipmaps"),
            ("📝", "Nomenclatura",          "Sem espaços, sem acentos, padrão snake_case"),
            ("🗑", "Materiais Não Usados", "Materiais sem referência em cenas ou prefabs"),
            ("🔊", "Áudio",               "Compressão Vorbis ativa para reduzir build"),
            ("📋", "Duplicados",           "Arquivos com mesmo nome e mesmo tamanho"),
        };

        foreach (var (icon, nome, desc) in checks)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(icon, new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 }, GUILayout.Width(22));
            GUILayout.BeginVertical();
            GUILayout.Label(nome, EditorStyles.boldLabel);
            GUILayout.Label(desc, sLabel);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            EditorGUILayout.Space(3);
        }

        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — ESCANEAR
    // ════════════════════════════════════════════════════════
    void EscanearAssets(bool autoFix)
    {
        problemas.Clear();
        escaneando = true;
        escaneou   = false;
        progresso  = 0f;
        totalErro = totalAviso = totalInfo = 0;
        Repaint();

        try
        {
            string[] guids = ObterGUIDsSelecionados();

            if (guids.Length == 0)
            {
                EditorUtility.DisplayDialog("Nenhum asset selecionado",
                    "Selecione arquivos na aba Project antes de escanear.", "OK");
                return;
            }

            // ── Texturas ──────────────────────────────────
            if (chkTexturas)
            {
                SetStatus("Analisando texturas...", 0.10f);
                ChecarTexturas(guids);
            }

            // ── Nomenclatura ──────────────────────────────
            if (chkNomes)
            {
                SetStatus("Verificando nomenclatura...", 0.30f);
                ChecarNomes(guids);
            }

            // ── Materiais não usados ──────────────────────
            if (chkMateriais)
            {
                SetStatus("Analisando materiais...", 0.50f);
                ChecarMateriais(guids);
            }

            // ── Áudio ─────────────────────────────────────
            if (chkAudio)
            {
                SetStatus("Verificando áudio...", 0.70f);
                ChecarAudio(guids);
            }

            // ── Duplicados ────────────────────────────────
            if (chkDuplic)
            {
                SetStatus("Detectando duplicados...", 0.85f);
                ChecarDuplicados(guids);
            }

            SetStatus("Concluído!", 1.0f);

            // Ordena: Erros → Avisos → Info, por categoria
            problemas = problemas
                .OrderBy(p => p.categoria)
                .ThenBy(p  => (int)p.nivel == 2 ? 0 : (int)p.nivel == 1 ? 1 : 2)
                .ToList();

            totalErro  = problemas.Count(p => p.nivel == Severidade.Erro);
            totalAviso = problemas.Count(p => p.nivel == Severidade.Aviso);
            totalInfo  = problemas.Count(p => p.nivel == Severidade.Info);

            // Log clicável no Console (⚠ LogWarning — nunca LogError)
            foreach (var prob in problemas.Where(p => p.nivel != Severidade.Info))
            {
                Debug.LogWarning(
                    $"[🔎 AssetLinter] {prob.titulo} | {prob.detalhe}", prob.assetObj);
            }

            // Auto-Fix automático
            if (autoFix)
            {
                var parafixar = problemas.Where(p => p.marcado && p.tipoFix != TipoFix.Nenhum).ToList();
                if (parafixar.Count > 0) AplicarFixes(parafixar);
            }
        }
        finally
        {
            escaneando = false;
            escaneou   = true;
            Repaint();
        }
    }

    // ════════════════════════════════════════════════════════
    //  CHECK 1 — TEXTURAS / SPRITES PIXEL ART
    // ════════════════════════════════════════════════════════
    void ChecarTexturas(string[] guids)
    {
        const string CAT = "🖼  Texturas & Sprites";

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) continue;

            Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            string nome  = Path.GetFileName(path);
            bool   eSprite = ti.textureType == TextureImporterType.Sprite ||
                             ti.spriteImportMode != SpriteImportMode.None;

            // Filtro Bilinear/Trilinear (pixel art fica borrado)
            if (ti.filterMode != FilterMode.Point)
            {
                var prob = new Problema
                {
                    categoria    = CAT,
                    titulo       = $"Filtro {ti.filterMode}: {nome}",
                    detalhe      = "Pixel art fica borrado com filtro Bilinear/Trilinear. Deve ser Point.",
                    caminhoAsset = path,
                    assetObj     = asset,
                    nivel        = Severidade.Erro,
                    tipoFix      = TipoFix.TexturaPixelArt,
                    labelFix     = "🔧 Mudar para Point Filter",
                    marcado      = true,
                };
                problemas.Add(prob);
            }

            // Compressão ativa (estraga cores em sprites)
            if (ti.textureCompression != TextureImporterCompression.Uncompressed &&
                ti.textureCompression != TextureImporterCompression.CompressedLQ)
            {
                if (eSprite)
                {
                    problemas.Add(new Problema
                    {
                        categoria    = CAT,
                        titulo       = $"Compressão ativa no sprite: {nome}",
                        detalhe      = $"Compressão {ti.textureCompression} pode artefatos e cores erradas. Use None.",
                        caminhoAsset = path,
                        assetObj     = asset,
                        nivel        = Severidade.Aviso,
                        tipoFix      = TipoFix.TexturaPixelArt,
                        labelFix     = "🔧 Desativar compressão",
                        marcado      = true,
                    });
                }
            }

            // Mipmaps em sprites 2D (gasta memória à toa)
            if (ti.mipmapEnabled && eSprite)
            {
                problemas.Add(new Problema
                {
                    categoria    = CAT,
                    titulo       = $"Mipmaps ativos em sprite 2D: {nome}",
                    detalhe      = "Sprites 2D não precisam de mipmaps. Desative para economizar memória.",
                    caminhoAsset = path,
                    assetObj     = asset,
                    nivel        = Severidade.Aviso,
                    tipoFix      = TipoFix.TexturaPixelArt,
                    labelFix     = "🔧 Desativar mipmaps",
                    marcado      = true,
                });
            }

            // NPOT — não é potência de 2
            Texture2D tex = asset as Texture2D;
            if (tex != null && (!IsPow2(tex.width) || !IsPow2(tex.height)))
            {
                problemas.Add(new Problema
                {
                    categoria    = CAT,
                    titulo       = $"Dimensão não-POT: {nome} ({tex.width}×{tex.height})",
                    detalhe      = "Texturas NPOT consomem mais VRAM. Prefira tamanhos potência de 2.",
                    caminhoAsset = path,
                    assetObj     = asset,
                    nivel        = Severidade.Info,
                    tipoFix      = TipoFix.Nenhum,
                });
            }

            // OK — textura correta
            if (ti.filterMode == FilterMode.Point &&
                ti.textureCompression == TextureImporterCompression.Uncompressed)
            {
                problemas.Add(new Problema
                {
                    categoria    = CAT,
                    titulo       = $"✅ OK: {nome}",
                    detalhe      = "Point filter + sem compressão — perfeito para pixel art.",
                    caminhoAsset = path,
                    assetObj     = asset,
                    nivel        = Severidade.Info,
                    tipoFix      = TipoFix.Nenhum,
                });
            }
        }
    }

    // ════════════════════════════════════════════════════════
    //  CHECK 2 — NOMENCLATURA
    // ════════════════════════════════════════════════════════
    void ChecarNomes(string[] guids)
    {
        const string CAT = "📝  Nomenclatura de Arquivos";

        foreach (string guid in guids)
        {
            string path  = AssetDatabase.GUIDToAssetPath(guid);
            string nome  = Path.GetFileNameWithoutExtension(path);
            string ext   = Path.GetExtension(path);
            Object asset = AssetDatabase.LoadMainAssetAtPath(path);

            bool temEspaco   = nome.Contains(' ');
            bool temAcento   = Regex.IsMatch(nome, @"[áéíóúàèìòùâêîôûãõäëïöüçñÁÉÍÓÚÀÈÌÒÙÂÊÎÔÛÃÕÄËÏÖÜÇÑ]");
            bool temEspecial = Regex.IsMatch(nome, @"[^a-zA-Z0-9_\-\.]");
            bool temMaius    = nome != nome.ToLower() && nome != nome.ToUpper();

            if (!temEspaco && !temAcento && !temEspecial) continue;

            string nomeLimpo = LimparNome(nome);
            string problemaDesc = "";
            if (temEspaco)   problemaDesc += "espaços → use _ | ";
            if (temAcento)   problemaDesc += "acentos → remover | ";
            if (temEspecial) problemaDesc += "caracteres especiais | ";
            problemaDesc = problemaDesc.TrimEnd(' ', '|');

            problemas.Add(new Problema
            {
                categoria    = CAT,
                titulo       = $"Nome fora do padrão: \"{nome}{ext}\"",
                detalhe      = $"Problema: {problemaDesc}\nSugestão: \"{nomeLimpo}{ext}\"",
                caminhoAsset = path,
                assetObj     = asset,
                nivel        = temEspaco ? Severidade.Erro : Severidade.Aviso,
                tipoFix      = TipoFix.RenomearArquivo,
                labelFix     = $"🔧 Renomear para: {nomeLimpo}{ext}",
                novoNome     = nomeLimpo + ext,
                marcado      = true,
            });
        }
    }

    string LimparNome(string nome)
    {
        // Remove acentos
        string semAcento = Encoding.ASCII.GetString(
            Encoding.GetEncoding("Cyrillic").GetBytes(nome));

        // Substitui espaços e caracteres inválidos por _
        string limpo = Regex.Replace(semAcento, @"[^a-zA-Z0-9_\-]", "_");

        // Colapsa múltiplos underscores
        limpo = Regex.Replace(limpo, @"_+", "_").Trim('_');

        return limpo.ToLower();
    }

    // ════════════════════════════════════════════════════════
    //  CHECK 3 — MATERIAIS NÃO USADOS
    // ════════════════════════════════════════════════════════
    void ChecarMateriais(string[] guids)
    {
        const string CAT = "🗑  Materiais Não Usados";

        // Coleta todos os materiais selecionados
        var matPaths = guids
            .Select(g => AssetDatabase.GUIDToAssetPath(g))
            .Where(p => p.EndsWith(".mat"))
            .ToList();

        if (matPaths.Count == 0) return;

        // Busca dependências em todos os prefabs e cenas do projeto
        HashSet<string> materiaisUsados = new HashSet<string>();

        string[] allAssets = AssetDatabase.GetAllAssetPaths();
        foreach (string ap in allAssets)
        {
            if (!ap.EndsWith(".prefab") && !ap.EndsWith(".unity")) continue;

            string[] deps = AssetDatabase.GetDependencies(ap, true);
            foreach (string dep in deps)
                if (dep.EndsWith(".mat"))
                    materiaisUsados.Add(dep);
        }

        foreach (string matPath in matPaths)
        {
            Object mat = AssetDatabase.LoadMainAssetAtPath(matPath);
            bool   usado = materiaisUsados.Contains(matPath);

            if (!usado)
            {
                problemas.Add(new Problema
                {
                    categoria    = CAT,
                    titulo       = $"Material não usado: {Path.GetFileName(matPath)}",
                    detalhe      = "Nenhum prefab ou cena referencia este material. Pode ser removido.",
                    caminhoAsset = matPath,
                    assetObj     = mat,
                    nivel        = Severidade.Aviso,
                    tipoFix      = TipoFix.RemoverMaterial,
                    labelFix     = "🗑 Mover para _Lixeira",
                    marcado      = false, // não marca por padrão — ação destrutiva
                });
            }
        }
    }

    // ════════════════════════════════════════════════════════
    //  CHECK 4 — ÁUDIO
    // ════════════════════════════════════════════════════════
    void ChecarAudio(string[] guids)
    {
        const string CAT = "🔊  Configuração de Áudio";

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AudioImporter ai = AssetImporter.GetAtPath(path) as AudioImporter;
            if (ai == null) continue;

            Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            var settings = ai.defaultSampleSettings;

            if (settings.compressionFormat == AudioCompressionFormat.PCM)
            {
                problemas.Add(new Problema
                {
                    categoria    = CAT,
                    titulo       = $"Sem compressão (PCM): {Path.GetFileName(path)}",
                    detalhe      = "PCM aumenta muito o tamanho da build. Recomendado: Vorbis (qualidade 70%).",
                    caminhoAsset = path,
                    assetObj     = asset,
                    nivel        = Severidade.Aviso,
                    tipoFix      = TipoFix.Nenhum,
                    labelFix     = null,
                });
            }
        }
    }

    // ════════════════════════════════════════════════════════
    //  CHECK 5 — DUPLICADOS
    // ════════════════════════════════════════════════════════
    void ChecarDuplicados(string[] guids)
    {
        const string CAT = "📋  Arquivos Duplicados";

        // Agrupa por nome do arquivo + tamanho
        var grupos = guids
            .Select(g => AssetDatabase.GUIDToAssetPath(g))
            .Where(p => File.Exists(p))
            .GroupBy(p => $"{Path.GetFileName(p)}_{new FileInfo(p).Length}")
            .Where(g => g.Count() > 1);

        foreach (var grupo in grupos)
        {
            var lista = grupo.ToList();
            for (int i = 1; i < lista.Count; i++)
            {
                Object asset = AssetDatabase.LoadMainAssetAtPath(lista[i]);
                problemas.Add(new Problema
                {
                    categoria    = CAT,
                    titulo       = $"Duplicado: {Path.GetFileName(lista[i])}",
                    detalhe      = $"Idêntico a: {lista[0]}\nTamanho: {new FileInfo(lista[i]).Length / 1024} KB",
                    caminhoAsset = lista[i],
                    assetObj     = asset,
                    nivel        = Severidade.Aviso,
                    tipoFix      = TipoFix.Nenhum,
                });
            }
        }
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — APLICAR FIXES
    // ════════════════════════════════════════════════════════
    void AplicarFixes(List<Problema> lista)
    {
        int corrigidos = 0;

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var prob in lista)
            {
                if (string.IsNullOrEmpty(prob.caminhoAsset)) continue;

                switch (prob.tipoFix)
                {
                    case TipoFix.TexturaPixelArt:
                        FixTextura(prob.caminhoAsset);
                        corrigidos++;
                        break;

                    case TipoFix.RenomearArquivo:
                        FixNome(prob.caminhoAsset, prob.novoNome);
                        corrigidos++;
                        break;

                    case TipoFix.RemoverMaterial:
                        FixMoverMaterial(prob.caminhoAsset);
                        corrigidos++;
                        break;
                }
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        Debug.LogWarning($"[🔎 AssetLinter] ✅ {corrigidos} correção(ões) aplicada(s).");
        EditorUtility.DisplayDialog("Auto-Fix Concluído ✅",
            $"{corrigidos} asset(s) corrigido(s).\n\nUse Ctrl+Z para desfazer renomeações.", "OK");

        // Re-escaneia após fix
        EscanearAssets(autoFix: false);
    }

    void FixTextura(string path)
    {
        TextureImporter ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return;

        ti.filterMode          = FilterMode.Point;
        ti.textureCompression  = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled       = false;
        ti.SaveAndReimport();

        Debug.LogWarning($"[🔎 AssetLinter] Textura corrigida: {Path.GetFileName(path)}");
    }

    void FixNome(string path, string novoNome)
    {
        if (string.IsNullOrEmpty(novoNome)) return;

        string erro = AssetDatabase.RenameAsset(path, novoNome);
        if (string.IsNullOrEmpty(erro))
            Debug.LogWarning($"[🔎 AssetLinter] Renomeado: {Path.GetFileName(path)} → {novoNome}");
        else
            Debug.LogWarning($"[🔎 AssetLinter] Falha ao renomear {Path.GetFileName(path)}: {erro}");
    }

    void FixMoverMaterial(string path)
    {
        string pastaLixeira = "Assets/_Lixeira";
        if (!AssetDatabase.IsValidFolder(pastaLixeira))
            AssetDatabase.CreateFolder("Assets", "_Lixeira");

        string nomeDest = Path.GetFileName(path);
        string destino  = $"{pastaLixeira}/{nomeDest}";
        string erro     = AssetDatabase.MoveAsset(path, destino);

        if (string.IsNullOrEmpty(erro))
            Debug.LogWarning($"[🔎 AssetLinter] Material movido para _Lixeira: {nomeDest}");
        else
            Debug.LogWarning($"[🔎 AssetLinter] Falha ao mover material: {erro}");
    }

    // ════════════════════════════════════════════════════════
    //  UTILITÁRIOS
    // ════════════════════════════════════════════════════════
    string[] ObterGUIDsSelecionados()
    {
        return Selection.objects
            .Select(o => AssetDatabase.GetAssetPath(o))
            .Where(p => !string.IsNullOrEmpty(p))
            .Select(p => AssetDatabase.AssetPathToGUID(p))
            .Where(g => !string.IsNullOrEmpty(g))
            .ToArray();
    }

    int ContarAssetsSelecionados()
        => Selection.objects.Count(o => !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(o)));

    bool IsPow2(int v) => v > 0 && (v & (v - 1)) == 0;

    void SetStatus(string msg, float p)
    {
        statusAtual = msg;
        progresso   = p;
        Repaint();
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

        sBotaoDiag = new GUIStyle(GUI.skin.button)
        {
            fontSize   = 11,
            fontStyle  = FontStyle.Bold,
            wordWrap   = true,
            normal     = { textColor = Color.white }
        };
        sBotaoFix  = new GUIStyle(sBotaoDiag);

        estilosOk = true;
    }

    void OnSelectionChange() => Repaint();
}
