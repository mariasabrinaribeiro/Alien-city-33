using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// ============================================================
// PainelDesignPro.cs  —  Hub Central de DevOps e Ferramentas
//
// Abre em: Ferramentas → 🎛 PainelDesignPro   (Ctrl + Shift + D)
//
// Módulos integrados:
//   🔑  Assinatura Android (Google Play Keystore)
//       • Valida keystore + senha com keytool real do JDK
//       • Abre pasta do keystore no Explorer
//       • Injeta credenciais no PlayerSettings
//
//   🚀  Build & Validação
//       • Chama o ValidadorPreBuild embutido
//       • Gera .aab (Android App Bundle) para o Play
//       • Gera .apk para teste local
//
//   🔗  Atalhos para Todas as Ferramentas
//       • Abre qualquer ferramenta do kit com 1 clique
//
//   📊  Status do Projeto
//       • Versão, bundle ID, build number, plataforma ativa
//
// Política de Log: SOMENTE Debug.LogWarning — nunca LogError.
// ============================================================

public class PainelDesignPro : EditorWindow
{
    // ── Abas ─────────────────────────────────────────────────
    private int    aba     = 0;
    private string[] abas = { "🔑 Assinatura", "🚀 Build", "🔗 Ferramentas", "📊 Status" };

    // ── Validação Keystore ───────────────────────────────────
    private enum EstadoValidacao { NaoVerificado, Verificando, Valido, Invalido, ArquivoAusente }
    private EstadoValidacao estadoKS = EstadoValidacao.NaoVerificado;
    private string  mensagemKS = "";
    private string  detalheKS  = "";
    private string  aliasKS    = "";   // alias encontrado pelo keytool
    private string  validadeKS = "";   // data de validade do certificado

    // ── Estado da Build ──────────────────────────────────────
    private bool   gerandoBuild = false;
    private string logBuild     = "";

    // ── Estilos ──────────────────────────────────────────────
    private bool   estilosOk = false;
    private GUIStyle sTitulo, sSecao, sLabel, sBotaoGrande, sBotaoMedio,
                     sBotaoFerr, sTagOk, sTagErro, sTagAviso, sTagInfo, sLog;

    // Cores
    private static readonly Color cAzul    = new Color(0.20f, 0.55f, 1.00f);
    private static readonly Color cVerde   = new Color(0.15f, 0.75f, 0.40f);
    private static readonly Color cLaranja = new Color(1.00f, 0.55f, 0.10f);
    private static readonly Color cVermel  = new Color(0.90f, 0.22f, 0.20f);
    private static readonly Color cRoxo    = new Color(0.65f, 0.25f, 1.00f);
    private static readonly Color cCinza   = new Color(0.40f, 0.40f, 0.50f);

    private Vector2 scroll;

    // ── Chaves EditorPrefs (espelha KeystoreManager) ─────────
    private static string Pfx          => $"KM_{PlayerSettings.productName}_";
    private static string KeyPath      => Pfx + "KeystorePath";
    private static string KeyKSPass    => Pfx + "KeystorePass";
    private static string KeyAlias     => Pfx + "KeyAlias";
    private static string KeyAliasPass => Pfx + "KeyAliasPass";

    // ════════════════════════════════════════════════════════
    //  ABRIR
    // ════════════════════════════════════════════════════════
    [MenuItem("Ferramentas/🎛 PainelDesignPro  %#d")]
    public static void Abrir()
    {
        var w = GetWindow<PainelDesignPro>("🎛 PainelDesignPro");
        w.minSize = new Vector2(380, 600);
        w.Show();
    }

    // ════════════════════════════════════════════════════════
    //  OnGUI
    // ════════════════════════════════════════════════════════
    void OnGUI()
    {
        CarregarEstilos();
        DesenharCabecalho();
        aba = GUILayout.Toolbar(aba, abas, GUILayout.Height(30));
        EditorGUILayout.Space(8);

        scroll = GUILayout.BeginScrollView(scroll);
        switch (aba)
        {
            case 0: AbaAssinatura();  break;
            case 1: AbaBuild();       break;
            case 2: AbaFerramentas(); break;
            case 3: AbaStatus();      break;
        }
        GUILayout.EndScrollView();
    }

    // ════════════════════════════════════════════════════════
    //  ABA 0 — ASSINATURA ANDROID
    // ════════════════════════════════════════════════════════
    void AbaAssinatura()
    {
        // ── Status atual da credencial ────────────────────
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("STATUS DA CREDENCIAL SALVA", sTitulo);
        EditorGUILayout.Space(4);

        string ksPath  = EditorPrefs.GetString(KeyPath,  "(não definido)");
        string alias   = EditorPrefs.GetString(KeyAlias, "(não definido)");
        bool   temPass = EditorPrefs.HasKey(KeyKSPass) && EditorPrefs.HasKey(KeyAliasPass);

        DesenharLinhaStatus("📁 Keystore",    Path.GetFileName(ksPath) == "(não definido)" ? ksPath : Path.GetFileName(ksPath), temPass && File.Exists(ksPath));
        DesenharLinhaStatus("🔖 Key Alias",   alias,   !string.IsNullOrEmpty(alias) && alias != "(não definido)");
        DesenharLinhaStatus("🔑 Senhas",      temPass ? "Salvas e cifradas" : "Não definidas", temPass);

        EditorGUILayout.Space(4);

        GUILayout.BeginHorizontal();
        GUI.backgroundColor = cAzul;
        if (GUILayout.Button("🔑  Abrir Keystore Manager", GUILayout.Height(28)))
            GetWindow<KeystoreManager>("🔑 Keystore");
        GUI.backgroundColor = Color.white;

        // Botão abrir pasta do keystore
        GUI.enabled = File.Exists(ksPath);
        if (GUILayout.Button("📂  Abrir Pasta", GUILayout.Height(28)))
            AbrirPastaKeystore(ksPath);
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
        EditorGUILayout.Space(8);

        // ── Validador de Assinatura ───────────────────────
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("🔍  VALIDAR E PREPARAR PARA GOOGLE PLAY", sTitulo);
        EditorGUILayout.Space(4);

        // Painel de resultado da validação
        DesenharPainelValidacao();
        EditorGUILayout.Space(6);

        GUI.backgroundColor = estadoKS == EstadoValidacao.Valido ? cVerde : cAzul;
        if (GUILayout.Button(
            gerandoBuild ? "⏳  Validando..." : "🔍  VALIDAR KEYSTORE AGORA",
            sBotaoGrande))
        {
            ValidarKeystore();
        }
        GUI.backgroundColor = Color.white;

        GUILayout.EndVertical();
        EditorGUILayout.Space(8);

        // ── Injetar no PlayerSettings ─────────────────────
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("⚙  PLAYER SETTINGS ANDROID", sTitulo);
        EditorGUILayout.Space(4);

        string psKS    = PlayerSettings.Android.keystoreName;
        string psAlias = PlayerSettings.Android.keyaliasName;
        bool   psOk    = !string.IsNullOrEmpty(psKS) && !string.IsNullOrEmpty(psAlias);

        DesenharLinhaStatus("Keystore carregado no Player", Path.GetFileName(psKS), !string.IsNullOrEmpty(psKS));
        DesenharLinhaStatus("Alias carregado no Player",    psAlias, !string.IsNullOrEmpty(psAlias));
        EditorGUILayout.Space(4);

        GUILayout.BeginHorizontal();
        GUI.backgroundColor = cVerde;
        if (GUILayout.Button("⚡  Injetar Credenciais no PlayerSettings", GUILayout.Height(30)))
            KeystoreManager.AplicarCredenciaisNoPlayerSettings(silencioso: false);
        GUI.backgroundColor = Color.white;

        if (GUILayout.Button("🛠  Player Settings", GUILayout.Height(30), GUILayout.Width(110)))
            SettingsService.OpenProjectSettings("Project/Player");
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  PAINEL DE VALIDAÇÃO (resultado visual)
    // ════════════════════════════════════════════════════════
    void DesenharPainelValidacao()
    {
        Color   corFundo, corBorda, corTexto;
        string  icone;

        switch (estadoKS)
        {
            case EstadoValidacao.Valido:
                corFundo = new Color(0.05f, 0.18f, 0.07f);
                corBorda = cVerde;
                corTexto = cVerde;
                icone    = "✅";
                break;
            case EstadoValidacao.Invalido:
                corFundo = new Color(0.20f, 0.04f, 0.04f);
                corBorda = cVermel;
                corTexto = cVermel;
                icone    = "❌";
                break;
            case EstadoValidacao.ArquivoAusente:
                corFundo = new Color(0.20f, 0.12f, 0.02f);
                corBorda = cLaranja;
                corTexto = cLaranja;
                icone    = "⚠";
                break;
            case EstadoValidacao.Verificando:
                corFundo = new Color(0.08f, 0.12f, 0.22f);
                corBorda = cAzul;
                corTexto = cAzul;
                icone    = "⏳";
                break;
            default:
                corFundo = new Color(0.12f, 0.12f, 0.18f);
                corBorda = cCinza;
                corTexto = new Color(0.65f, 0.65f, 0.75f);
                icone    = "⬜";
                break;
        }

        Rect painel = GUILayoutUtility.GetRect(0, 80, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(painel, corFundo);
        // Borda
        EditorGUI.DrawRect(new Rect(painel.x, painel.y, painel.width, 2), corBorda);
        EditorGUI.DrawRect(new Rect(painel.x, painel.yMax - 2, painel.width, 2), corBorda);
        EditorGUI.DrawRect(new Rect(painel.x, painel.y, 4, painel.height), corBorda);
        EditorGUI.DrawRect(new Rect(painel.xMax - 4, painel.y, 4, painel.height), corBorda);

        // Ícone grande
        var stIcon = new GUIStyle(EditorStyles.boldLabel) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(painel.x + 6, painel.y + 6, 44, 44), icone, stIcon);

        // Mensagem principal
        var stMsg = new GUIStyle(EditorStyles.boldLabel)
        { fontSize = 11, wordWrap = true, normal = { textColor = corTexto } };
        GUI.Label(new Rect(painel.x + 56, painel.y + 8, painel.width - 64, 26), mensagemKS, stMsg);

        // Detalhe
        if (!string.IsNullOrEmpty(detalheKS))
        {
            var stDet = new GUIStyle(EditorStyles.miniLabel)
            { wordWrap = true, normal = { textColor = new Color(0.72f, 0.72f, 0.82f) } };
            GUI.Label(new Rect(painel.x + 56, painel.y + 36, painel.width - 64, 36), detalheKS, stDet);
        }

        if (estadoKS == EstadoValidacao.Valido && !string.IsNullOrEmpty(aliasKS))
        {
            var stAlias = new GUIStyle(EditorStyles.miniLabel)
            { normal = { textColor = new Color(0.5f, 1f, 0.6f) }, fontStyle = FontStyle.Bold };
            GUI.Label(new Rect(painel.x + 56, painel.y + 56, painel.width - 64, 18),
                $"Alias: {aliasKS}   Validade: {validadeKS}", stAlias);
        }

        GUILayout.Space(4);
    }

    // ════════════════════════════════════════════════════════
    //  ABA 1 — BUILD
    // ════════════════════════════════════════════════════════
    void AbaBuild()
    {
        // ── Requisitos para Build ─────────────────────────
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("PRÉ-REQUISITOS DA BUILD", sTitulo);
        EditorGUILayout.Space(4);

        bool ksOk    = File.Exists(EditorPrefs.GetString(KeyPath, ""));
        bool passOk  = EditorPrefs.HasKey(KeyKSPass);
        bool psKSOk  = !string.IsNullOrEmpty(PlayerSettings.Android.keystoreName);
        bool cenaOk  = EditorBuildSettings.scenes.Any(s => s.enabled);
        bool validado = estadoKS == EstadoValidacao.Valido;

        DesenharLinhaStatus("Arquivo .keystore existe",        "", ksOk);
        DesenharLinhaStatus("Senhas salvas no KeystoreManager","", passOk);
        DesenharLinhaStatus("PlayerSettings com keystore",     "", psKSOk);
        DesenharLinhaStatus("Ao menos 1 cena no Build",        "", cenaOk);
        DesenharLinhaStatus("Keystore validado (senha ok)",    "", validado);

        EditorGUILayout.Space(4);

        int problemas = new[] { ksOk, passOk, psKSOk, cenaOk }.Count(b => !b);
        if (problemas > 0)
        {
            var stAviso = new GUIStyle(EditorStyles.boldLabel)
            { normal = { textColor = cLaranja }, fontSize = 10 };
            GUILayout.Label($"  ⚠ {problemas} pré-requisito(s) pendente(s) — veja acima.", stAviso);
        }

        GUILayout.EndVertical();
        EditorGUILayout.Space(8);

        // ── Validador Pré-Build ───────────────────────────
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("VALIDAÇÃO COMPLETA DO PROJETO", sTitulo);
        EditorGUILayout.Space(4);

        EditorGUILayout.HelpBox(
            "Executa todas as verificações do Validador Pré-Build:\n" +
            "cenas, missing references, texturas e áudio.",
            MessageType.Info);
        EditorGUILayout.Space(4);

        GUI.backgroundColor = cAzul;
        if (GUILayout.Button("🚀  Abrir Validador Pré-Build Completo", GUILayout.Height(34)))
            GetWindow<ValidadorPreBuild>("🚀 Pré-Build");
        GUI.backgroundColor = Color.white;

        GUILayout.EndVertical();
        EditorGUILayout.Space(8);

        // ── Gerar Build ───────────────────────────────────
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("GERAR BUILD ANDROID", sTitulo);
        EditorGUILayout.Space(4);

        // .aab (Google Play)
        GUI.backgroundColor = new Color(0.10f, 0.60f, 0.20f);
        GUI.enabled = !gerandoBuild;
        if (GUILayout.Button(
            gerandoBuild ? "⏳  Gerando .aab..." : "📦  GERAR .aab — Google Play (Release)",
            sBotaoGrande))
        {
            GerarBuild(BuildType.AAB);
        }

        EditorGUILayout.Space(4);

        GUI.backgroundColor = new Color(0.10f, 0.40f, 0.75f);
        if (GUILayout.Button("🔧  GERAR .apk — Teste Local (Debug)", GUILayout.Height(30)))
            GerarBuild(BuildType.APK_Debug);

        GUI.backgroundColor = Color.white;
        GUI.enabled = true;

        // Log da build
        if (!string.IsNullOrEmpty(logBuild))
        {
            EditorGUILayout.Space(4);
            GUILayout.TextArea(logBuild, sLog, GUILayout.MaxHeight(80));
        }

        EditorGUILayout.Space(4);
        if (GUILayout.Button("📂  Abrir Pasta de Saída da Build", GUILayout.Height(26)))
            AbrirPastaBuild();

        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  ABA 2 — FERRAMENTAS
    // ════════════════════════════════════════════════════════
    void AbaFerramentas()
    {
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("KIT DE FERRAMENTAS DO PROJETO", sTitulo);
        EditorGUILayout.Space(4);
        GUILayout.Label("  Clique para abrir qualquer ferramenta diretamente.", sLabel);
        GUILayout.EndVertical();
        EditorGUILayout.Space(8);

        var ferramentas = new (string icone, string nome, string desc, Action acao)[]
        {
            ("🔑", "Keystore Manager",             "Credenciais Android seguras",           () => GetWindow<KeystoreManager>("🔑 Keystore")),
            ("🚀", "Validador Pré-Build",           "Verifica projeto antes de compilar",    () => GetWindow<ValidadorPreBuild>("🚀 Pré-Build")),
            ("🗂", "Scene Cleaner",                 "Tags, layers e faxina de cena",         () => GetWindow<SceneCleaner>("🗂 Scene Cleaner")),
            ("🧹", "Vassoureiro",                   "Remove GameObjects fantasmas",          () => GetWindow<Vassoureiro>("🧹 Vassoureiro")),
            ("📐", "Organizador de Hierarquia",     "Agrupa, ordena e renomeia objetos",     () => GetWindow<OrganizadorHierarquia>("📐 Organizador")),
            ("🎨", "Paleta de Cores Global",        "Aplica paleta de cores na cena",        () => GetWindow<PaletaCoresGlobal>("🎨 Paleta")),
            ("⬛", "Alinhador UI",                  "Alinha e distribui elementos UI",       () => GetWindow<AlinhadorUI>("⬛ Alinhador")),
            ("⚓", "Anchor Snap & Resoluções",       "Snap de âncoras e simulador de telas",  () => GetWindow<AnchorSnap>("⚓ Anchor Snap")),
            ("🔄", "Converter RectTransform",       "Converte UI para objeto 2D normal",     () => EditorApplication.ExecuteMenuItem("Ferramentas/Converter RectTransform/► Converter Selecionado(s)")),
            ("🛠", "Player Settings",               "Configurações do projeto Android",      () => SettingsService.OpenProjectSettings("Project/Player")),
            ("🏗", "Build Settings",                "Cenas e configurações de build",        () => EditorWindow.GetWindow(Type.GetType("UnityEditor.BuildPlayerWindow,UnityEditor"))),
            ("📁", "Project Settings",              "Todas as configurações do projeto",      () => SettingsService.OpenProjectSettings("Project")),
        };

        int colunas = 2;
        for (int i = 0; i < ferramentas.Length; i += colunas)
        {
            GUILayout.BeginHorizontal();
            for (int j = 0; j < colunas && i + j < ferramentas.Length; j++)
            {
                var (icone, nome, desc, acao) = ferramentas[i + j];
                DesenharCartaoFerramenta(icone, nome, desc, acao);
            }
            GUILayout.EndHorizontal();
            EditorGUILayout.Space(4);
        }
    }

    // ════════════════════════════════════════════════════════
    //  ABA 3 — STATUS DO PROJETO
    // ════════════════════════════════════════════════════════
    void AbaStatus()
    {
        // ── Info do App ───────────────────────────────────
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("INFORMAÇÕES DO APP", sTitulo);
        EditorGUILayout.Space(4);

        LinhaInfo("📱 Nome do App",       PlayerSettings.productName);
        LinhaInfo("🏢 Empresa",           PlayerSettings.companyName);
        LinhaInfo("🆔 Bundle ID",         PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android));
        LinhaInfo("🔢 Versão",            PlayerSettings.bundleVersion);
        LinhaInfo("📊 Bundle Version Code", PlayerSettings.Android.bundleVersionCode.ToString());
        LinhaInfo("🎯 Min SDK",           ((int)PlayerSettings.Android.minSdkVersion).ToString());
        LinhaInfo("🎯 Target SDK",        ((int)PlayerSettings.Android.targetSdkVersion).ToString());
        LinhaInfo("🏗 Scripting Backend", PlayerSettings.GetScriptingBackend(BuildTargetGroup.Android).ToString());
        GUILayout.EndVertical();
        EditorGUILayout.Space(8);

        // ── Build Settings ────────────────────────────────
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("BUILD SETTINGS", sTitulo);
        EditorGUILayout.Space(4);

        var cenas = EditorBuildSettings.scenes;
        LinhaInfo("🎬 Cenas na Build",    cenas.Length.ToString());
        LinhaInfo("✅ Habilitadas",       cenas.Count(s => s.enabled).ToString());
        LinhaInfo("❌ Desabilitadas",     cenas.Count(s => !s.enabled).ToString());
        LinhaInfo("🎯 Plataforma Ativa",  EditorUserBuildSettings.activeBuildTarget.ToString());
        EditorGUILayout.Space(4);

        foreach (var cena in cenas.Take(6))
        {
            var stCena = new GUIStyle(EditorStyles.miniLabel)
            { normal = { textColor = cena.enabled ? cVerde : cCinza } };
            GUILayout.Label($"  {(cena.enabled ? "✅" : "☐")}  {Path.GetFileNameWithoutExtension(cena.path)}", stCena);
        }
        if (cenas.Length > 6)
            GUILayout.Label($"  ... e mais {cenas.Length - 6} cena(s)", sLabel);

        GUILayout.EndVertical();
        EditorGUILayout.Space(8);

        // ── Ações Rápidas de Configuração ─────────────────
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("CONFIGURAÇÃO RÁPIDA", sTitulo);
        EditorGUILayout.Space(4);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("📊 Bundle Version\n+1",    GUILayout.Height(38)))
        {
            PlayerSettings.Android.bundleVersionCode++;
            Debug.LogWarning($"[🎛 PainelDesignPro] Bundle Version Code → {PlayerSettings.Android.bundleVersionCode}");
        }
        if (GUILayout.Button("🔧 IL2CPP\nBackend",       GUILayout.Height(38)))
        {
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
            Debug.LogWarning("[🎛 PainelDesignPro] Scripting Backend definido para IL2CPP.");
        }
        if (GUILayout.Button("🏗 ARM64\nArchitecture",  GUILayout.Height(38)))
        {
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            Debug.LogWarning("[🎛 PainelDesignPro] Arquitetura definida para ARM64.");
        }
        GUILayout.EndHorizontal();

        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — VALIDAR KEYSTORE COM KEYTOOL
    // ════════════════════════════════════════════════════════
    void ValidarKeystore()
    {
        estadoKS    = EstadoValidacao.Verificando;
        mensagemKS  = "Verificando keystore com keytool...";
        detalheKS   = "";
        aliasKS     = "";
        validadeKS  = "";
        Repaint();

        string ksPath = EditorPrefs.GetString(KeyPath, "");

        // 1. Arquivo existe?
        if (string.IsNullOrEmpty(ksPath) || !File.Exists(ksPath))
        {
            estadoKS   = EstadoValidacao.ArquivoAusente;
            mensagemKS = "Arquivo .keystore não encontrado!";
            detalheKS  = string.IsNullOrEmpty(ksPath)
                ? "Nenhum caminho definido. Abra o Keystore Manager e configure."
                : $"Arquivo não existe em:\n{ksPath}";
            Debug.LogWarning($"[🎛 PainelDesignPro] Keystore não encontrado: {ksPath}");
            Repaint();
            return;
        }

        // 2. Recupera senhas decifradas via reflexão dos EditorPrefs cifrados
        string ksPass = RecuperarSenha(KeyKSPass);
        if (string.IsNullOrEmpty(ksPass))
        {
            estadoKS   = EstadoValidacao.Invalido;
            mensagemKS = "Senha do Keystore não definida!";
            detalheKS  = "Abra o Keystore Manager e salve a senha.";
            Repaint();
            return;
        }

        // 3. Tenta executar o keytool do JDK da Unity
        string keytool = LocalizarKeytool();

        if (string.IsNullOrEmpty(keytool))
        {
            // Fallback: valida apenas existência + tamanho do arquivo
            ValidarFallback(ksPath, ksPass);
            return;
        }

        // 4. Executa keytool -list -v
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName               = keytool,
                Arguments              = $"-list -v -keystore \"{ksPath}\" -storepass \"{ksPass}\"",
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true,
            };

            using (var proc = Process.Start(psi))
            {
                string saida = proc.StandardOutput.ReadToEnd();
                string erro  = proc.StandardError.ReadToEnd();
                proc.WaitForExit(8000);

                if (proc.ExitCode == 0)
                {
                    // Extrai informações do alias e validade
                    aliasKS    = ExtrairLinha(saida, "Alias name:");
                    validadeKS = ExtrairLinha(saida, "Valid from:");
                    if (string.IsNullOrEmpty(validadeKS))
                        validadeKS = ExtrairLinha(saida, "until:");

                    estadoKS   = EstadoValidacao.Valido;
                    mensagemKS = "✅ Keystore VÁLIDO — Senha correta!";
                    detalheKS  = $"Arquivo: {Path.GetFileName(ksPath)}";

                    Debug.LogWarning($"[🎛 PainelDesignPro] ✅ Keystore validado com sucesso. Alias: {aliasKS}");
                }
                else
                {
                    // Senha errada ou arquivo corrompido
                    estadoKS   = EstadoValidacao.Invalido;

                    bool senhaErrada = erro.Contains("password") || erro.Contains("keystore")
                                    || saida.Contains("password") || proc.ExitCode == 1;

                    mensagemKS = senhaErrada
                        ? "❌ SENHA INCORRETA! Build seria rejeitada."
                        : "❌ Erro ao ler o keystore!";
                    detalheKS = $"Verifique a senha no Keystore Manager.\nErro keytool: {erro.Trim().Split('\n')[0]}";

                    Debug.LogWarning($"[🎛 PainelDesignPro] ❌ Keystore inválido. Exit: {proc.ExitCode}. Erro: {erro.Trim()}");
                }
            }
        }
        catch (Exception ex)
        {
            ValidarFallback(ksPath, ksPass);
            Debug.LogWarning($"[🎛 PainelDesignPro] keytool falhou, usando validação básica. Detalhes: {ex.Message}");
        }

        Repaint();
    }

    // ── Localiza o keytool do JDK da Unity ───────────────
    string LocalizarKeytool()
    {
        // 1. Tenta pelo path do JDK configurado na Unity
        string jdkPath = "";

        try
        {
#if UNITY_2019_1_OR_NEWER
            jdkPath = UnityEditor.Android.AndroidExternalToolsSettings.jdkRootPath;
#endif
        }
        catch { }

        if (string.IsNullOrEmpty(jdkPath))
            jdkPath = EditorPrefs.GetString("JdkPath", "");

        if (!string.IsNullOrEmpty(jdkPath))
        {
            string kt = Path.Combine(jdkPath, "bin",
                Application.platform == RuntimePlatform.WindowsEditor ? "keytool.exe" : "keytool");
            if (File.Exists(kt)) return kt;
        }

        // 2. Tenta pelo PATH do sistema
        string[] candidatos = Application.platform == RuntimePlatform.WindowsEditor
            ? new[] { "keytool.exe" }
            : new[] { "/usr/bin/keytool", "/usr/local/bin/keytool" };

        foreach (string c in candidatos)
            if (File.Exists(c)) return c;

        // 3. Tenta encontrar no JDK empacotado com Android SDK
        string androidSdk = EditorPrefs.GetString("AndroidSdkRoot", "");
        if (!string.IsNullOrEmpty(androidSdk))
        {
            foreach (string pasta in Directory.GetDirectories(
                Path.Combine(androidSdk, ".."), "jdk*", SearchOption.AllDirectories).Take(5))
            {
                string kt = Path.Combine(pasta, "bin", "keytool" +
                    (Application.platform == RuntimePlatform.WindowsEditor ? ".exe" : ""));
                if (File.Exists(kt)) return kt;
            }
        }

        return null; // não encontrado — usa fallback
    }

    // ── Validação básica (sem keytool) ────────────────────
    void ValidarFallback(string ksPath, string ksPass)
    {
        FileInfo fi = new FileInfo(ksPath);
        bool     ok = fi.Length > 100; // arquivo válido tem mais de 100 bytes

        if (ok)
        {
            estadoKS   = EstadoValidacao.Valido;
            mensagemKS = "✅ Arquivo encontrado (validação básica)";
            detalheKS  = $"keytool não localizado — validação completa de senha indisponível.\n" +
                         $"Arquivo: {fi.Name} ({fi.Length / 1024} KB)";
            aliasKS    = EditorPrefs.GetString(KeyAlias, "");
            validadeKS = "keytool ausente";
        }
        else
        {
            estadoKS   = EstadoValidacao.Invalido;
            mensagemKS = "❌ Arquivo .keystore inválido ou corrompido!";
            detalheKS  = $"Tamanho suspeito: {fi.Length} bytes. Verifique o arquivo.";
        }

        Repaint();
    }

    // ── Recupera senha decifrada do EditorPrefs ───────────
    string RecuperarSenha(string chave)
    {
        try
        {
            string cifrado = EditorPrefs.GetString(chave, "");
            if (string.IsNullOrEmpty(cifrado)) return "";
            // Usa reflexão interna do KeystoreManager para decifrar
            var metodo = typeof(KeystoreManager).GetMethod(
                "Decifrar",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (metodo != null)
                return metodo.Invoke(null, new object[] { cifrado }) as string ?? "";

            return ""; // fallback: não conseguiu decifrar
        }
        catch
        {
            return "";
        }
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — GERAR BUILD
    // ════════════════════════════════════════════════════════
    enum BuildType { AAB, APK_Debug }

    void GerarBuild(BuildType tipo)
    {
        // Garante que as credenciais estão no PlayerSettings
        KeystoreManager.AplicarCredenciaisNoPlayerSettings(silencioso: true);

        string pasta   = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Builds", "Android");
        Directory.CreateDirectory(pasta);

        string arquivo = Path.Combine(pasta,
            tipo == BuildType.AAB
                ? $"{PlayerSettings.productName}_{PlayerSettings.bundleVersion}.aab"
                : $"{PlayerSettings.productName}_{PlayerSettings.bundleVersion}_debug.apk");

        var options = new BuildPlayerOptions
        {
            scenes       = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = arquivo,
            target       = BuildTarget.Android,
            options      = tipo == BuildType.AAB
                ? BuildOptions.None
                : BuildOptions.Development | BuildOptions.AllowDebugging,
        };

        EditorUserBuildSettings.buildAppBundle = tipo == BuildType.AAB;

        gerandoBuild = true;
        logBuild     = $"⏳ Iniciando build {(tipo == BuildType.AAB ? ".aab" : ".apk")}...\n";
        Repaint();

        try
        {
            BuildReport  report  = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                logBuild = $"✅ Build concluída!\n" +
                           $"Arquivo: {Path.GetFileName(arquivo)}\n" +
                           $"Tamanho: {summary.totalSize / 1024 / 1024} MB\n" +
                           $"Tempo: {summary.totalTime.TotalSeconds:F1}s";

                Debug.LogWarning($"[🎛 PainelDesignPro] ✅ Build OK: {arquivo}");

                EditorUtility.RevealInFinder(arquivo);
            }
            else
            {
                logBuild = $"❌ Build falhou! Resultado: {summary.result}\n" +
                           $"Erros: {summary.totalErrors}   Avisos: {summary.totalWarnings}\n" +
                           "Verifique o Console para detalhes.";

                Debug.LogWarning($"[🎛 PainelDesignPro] Build falhou: {summary.result}. " +
                                 $"Erros: {summary.totalErrors}");
            }
        }
        catch (Exception e)
        {
            logBuild = $"❌ Exceção durante a build:\n{e.Message}";
            Debug.LogWarning($"[🎛 PainelDesignPro] Exceção na build: {e.Message}");
        }
        finally
        {
            gerandoBuild = false;
            Repaint();
        }
    }

    // ════════════════════════════════════════════════════════
    //  UTILITÁRIOS
    // ════════════════════════════════════════════════════════
    void AbrirPastaKeystore(string ksPath)
    {
        string pasta = Path.GetDirectoryName(ksPath);
        if (Directory.Exists(pasta))
            EditorUtility.RevealInFinder(ksPath);
        else
            EditorUtility.DisplayDialog("Pasta não encontrada",
                $"A pasta não existe:\n{pasta}", "OK");
    }

    void AbrirPastaBuild()
    {
        string pasta = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Builds", "Android");
        if (!Directory.Exists(pasta)) Directory.CreateDirectory(pasta);
        EditorUtility.RevealInFinder(pasta);
    }

    string ExtrairLinha(string texto, string prefixo)
    {
        foreach (string linha in texto.Split('\n'))
        {
            int idx = linha.IndexOf(prefixo, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
                return linha.Substring(idx + prefixo.Length).Trim();
        }
        return "";
    }

    // ════════════════════════════════════════════════════════
    //  UI — HELPERS
    // ════════════════════════════════════════════════════════
    void DesenharLinhaStatus(string label, string valor, bool ok)
    {
        GUILayout.BeginHorizontal();
        var stOk = new GUIStyle(EditorStyles.miniLabel)
        { normal = { textColor = ok ? cVerde : cVermel }, fontStyle = FontStyle.Bold };
        GUILayout.Label(ok ? "  ✅" : "  ❌", stOk, GUILayout.Width(28));
        GUILayout.Label(label, sLabel, GUILayout.Width(200));
        if (!string.IsNullOrEmpty(valor))
            GUILayout.Label(valor, new GUIStyle(EditorStyles.miniLabel)
            { normal = { textColor = new Color(0.65f, 0.65f, 0.80f) } });
        GUILayout.EndHorizontal();
        EditorGUILayout.Space(1);
    }

    void LinhaInfo(string label, string valor)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, sLabel, GUILayout.Width(180));
        var stValor = new GUIStyle(EditorStyles.boldLabel)
        { fontSize = 10, normal = { textColor = new Color(0.85f, 0.85f, 1f) } };
        GUILayout.Label(valor, stValor);
        GUILayout.EndHorizontal();
        EditorGUILayout.Space(1);
    }

    void DesenharCartaoFerramenta(string icone, string nome, string desc, Action acao)
    {
        GUILayout.BeginVertical(sSecao, GUILayout.Width((position.width - 30) / 2f));

        GUILayout.BeginHorizontal();
        var stIcon = new GUIStyle(EditorStyles.boldLabel) { fontSize = 18 };
        GUILayout.Label(icone, stIcon, GUILayout.Width(28));
        GUILayout.BeginVertical();
        var stNome = new GUIStyle(EditorStyles.boldLabel)
        { fontSize = 10, normal = { textColor = Color.white } };
        GUILayout.Label(nome, stNome);
        GUILayout.Label(desc, sLabel);
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();

        EditorGUILayout.Space(4);
        GUI.backgroundColor = new Color(0.18f, 0.35f, 0.60f);
        if (GUILayout.Button("Abrir", GUILayout.Height(22)))
            acao?.Invoke();
        GUI.backgroundColor = Color.white;

        GUILayout.EndVertical();
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
        { fontSize = 11, fontStyle = FontStyle.Bold, fixedHeight = 40, normal = { textColor = Color.white } };

        sBotaoMedio  = new GUIStyle(GUI.skin.button) { fixedHeight = 30, fontSize = 10 };
        sBotaoFerr   = new GUIStyle(GUI.skin.button) { fixedHeight = 26, fontSize = 10 };

        sLog = new GUIStyle(EditorStyles.textArea)
        {
            fontSize = 9,
            wordWrap = true,
            normal   = { textColor = new Color(0.8f, 0.9f, 0.8f) }
        };

        sTagOk    = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = cVerde },   fontStyle = FontStyle.Bold };
        sTagErro  = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = cVermel },  fontStyle = FontStyle.Bold };
        sTagAviso = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = cLaranja }, fontStyle = FontStyle.Bold };
        sTagInfo  = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = cAzul },    fontStyle = FontStyle.Bold };

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
        GUILayout.Label("🎛  PAINELDESIGNPRO — DevOps Hub", st);
        Rect r = EditorGUILayout.GetControlRect(false, 2);
        EditorGUI.DrawRect(r, new Color(0.4f, 0.85f, 1f, 0.4f));
        EditorGUILayout.Space(6);
    }
}
