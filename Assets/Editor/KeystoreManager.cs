using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;

// ============================================================
// KeystoreManager.cs  —  Ferramenta de Editor
//
// Abre em: Ferramentas → 🔑 Keystore Manager
//          Atalho: Ctrl + Shift + K
//
// O que faz:
//   • Salva keystorePass e keyaliasPass localmente usando
//     EditorPrefs + ofuscação com DPAPI (Windows) ou AES (Mac/Linux)
//   • Preenche automaticamente os campos do PlayerSettings.Android
//     toda vez que o editor abre ([InitializeOnLoad])
//   • NUNCA salva senhas em texto puro — usa criptografia da máquina
//   • Não interfere com o git (EditorPrefs é local por máquina)
//
// Segurança:
//   • Windows: usa DPAPI (Data Protection API do SO — criptografia
//     vinculada ao usuário e à máquina, sem chave visível)
//   • Mac/Linux: usa AES-256 com salt derivado do nome da máquina
//   • As chaves no EditorPrefs contêm o nome do projeto para
//     evitar colisão entre projetos diferentes no mesmo computador
// ============================================================

// Carrega e aplica credenciais automaticamente ao abrir o editor
[InitializeOnLoad]
public class KeystoreAutoLoad
{
    static KeystoreAutoLoad()
    {
        // Pequeno delay para garantir que o editor terminou de inicializar
        EditorApplication.delayCall += () =>
        {
            try
            {
                KeystoreManager.AplicarCredenciaisNoPlayerSettings(silencioso: true);
            }
            catch (Exception e)
            {
                // ⚠ LogWarning apenas — nunca LogError
                Debug.LogWarning($"[🔑 KeystoreManager] Aviso ao carregar credenciais automáticas: {e.Message}");
            }
        };
    }
}

public class KeystoreManager : EditorWindow
{
    // ── Campos de entrada ────────────────────────────────────
    private string keystorePath     = "";
    private string keystorePass     = "";
    private string keyAlias         = "";
    private string keyAliasPass     = "";

    // ── UI ───────────────────────────────────────────────────
    private bool   mostrarKSPass    = false;
    private bool   mostrarAliasPass = false;
    private bool   estilosOk        = false;
    private bool   credenciaisOk    = false;
    private string mensagemStatus   = "";
    private Color  corStatus        = Color.gray;
    private Vector2 scroll;

    // ── Estilos ──────────────────────────────────────────────
    private GUIStyle sTitulo, sSecao, sLabel, sLabelPerigo, sBotaoGrande,
                     sBotaoVerde, sBotaoAzul, sBotaoVermelho, sBotaoSenha;

    // Cores
    private static readonly Color cAzul    = new Color(0.20f, 0.55f, 1.00f);
    private static readonly Color cVerde   = new Color(0.15f, 0.75f, 0.40f);
    private static readonly Color cLaranja = new Color(1.00f, 0.55f, 0.10f);
    private static readonly Color cVermel  = new Color(0.90f, 0.22f, 0.20f);
    private static readonly Color cAmarelo = new Color(0.95f, 0.85f, 0.10f);

    // ── Chaves EditorPrefs ───────────────────────────────────
    // Inclui nome do produto para evitar colisão entre projetos
    private static string Prefixo => $"KM_{PlayerSettings.productName}_";
    private static string KeyPath       => Prefixo + "KeystorePath";
    private static string KeyKSPass     => Prefixo + "KeystorePass";
    private static string KeyAlias      => Prefixo + "KeyAlias";
    private static string KeyAliasPass  => Prefixo + "KeyAliasPass";

    // ════════════════════════════════════════════════════════
    //  ABRIR
    // ════════════════════════════════════════════════════════
    [MenuItem("Ferramentas/🔑 Keystore Manager  %#k")]
    public static void Abrir()
    {
        var w = GetWindow<KeystoreManager>("🔑 Keystore");
        w.minSize = new Vector2(360, 580);
        w.CarregarCredenciais();
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
        DesenharStatusAutoLoad();
        EditorGUILayout.Space(8);

        DesenharSecaoKeystore();
        EditorGUILayout.Space(8);

        DesenharSecaoAlias();
        EditorGUILayout.Space(8);

        DesenharSecaoAcoes();
        EditorGUILayout.Space(8);

        DesenharSecaoSeguranca();
        EditorGUILayout.Space(8);

        DesenharSecaoGitIgnore();

        GUILayout.EndScrollView();
    }

    // ════════════════════════════════════════════════════════
    //  STATUS DE AUTO-CARREGAMENTO
    // ════════════════════════════════════════════════════════
    void DesenharStatusAutoLoad()
    {
        bool temCredenciais = EditorPrefs.HasKey(KeyKSPass) && EditorPrefs.HasKey(KeyAliasPass);

        if (temCredenciais)
        {
            var stOk = new GUIStyle(EditorStyles.helpBox)
            {
                padding = new RectOffset(10, 10, 6, 6)
            };
            GUILayout.BeginVertical(stOk);
            var stLabel = new GUIStyle(EditorStyles.boldLabel)
            { normal = { textColor = cVerde }, fontSize = 10 };
            GUILayout.Label("✅  Credenciais salvas — Carregadas automaticamente no PlayerSettings", stLabel);
            GUILayout.EndVertical();
        }
        else
        {
            EditorGUILayout.HelpBox(
                "⚠ Nenhuma credencial salva ainda. Preencha os campos abaixo e clique em Salvar.",
                MessageType.Warning);
        }
    }

    // ════════════════════════════════════════════════════════
    //  SEÇÃO KEYSTORE
    // ════════════════════════════════════════════════════════
    void DesenharSecaoKeystore()
    {
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("🗂  KEYSTORE", sTitulo);
        EditorGUILayout.Space(6);

        // Caminho do arquivo .keystore
        GUILayout.Label("Caminho do arquivo (.keystore):", sLabel);
        GUILayout.BeginHorizontal();
        keystorePath = EditorGUILayout.TextField(keystorePath);
        if (GUILayout.Button("📂", GUILayout.Width(28), GUILayout.Height(18)))
        {
            string selecionado = EditorUtility.OpenFilePanel(
                "Selecionar Keystore", Application.dataPath, "keystore,jks");
            if (!string.IsNullOrEmpty(selecionado))
                keystorePath = selecionado;
        }
        GUILayout.EndHorizontal();

        // Valida se o arquivo existe
        if (!string.IsNullOrEmpty(keystorePath))
        {
            bool existe = File.Exists(keystorePath);
            var stInfo  = new GUIStyle(EditorStyles.miniLabel)
            { normal = { textColor = existe ? cVerde : cVermel } };
            GUILayout.Label(existe ? "  ✅ Arquivo encontrado" : "  ❌ Arquivo NÃO encontrado!", stInfo);
        }

        EditorGUILayout.Space(8);

        // Senha do Keystore
        GUILayout.Label("Senha do Keystore (Keystore Password):", sLabel);
        GUILayout.BeginHorizontal();
        if (mostrarKSPass)
            keystorePass = EditorGUILayout.TextField(keystorePass);
        else
            keystorePass = EditorGUILayout.PasswordField(keystorePass);

        if (GUILayout.Button(mostrarKSPass ? "🙈" : "👁", GUILayout.Width(28), GUILayout.Height(18)))
            mostrarKSPass = !mostrarKSPass;
        GUILayout.EndHorizontal();

        DesenharForcaSenha(keystorePass);

        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  SEÇÃO KEY ALIAS
    // ════════════════════════════════════════════════════════
    void DesenharSecaoAlias()
    {
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("🔖  KEY ALIAS", sTitulo);
        EditorGUILayout.Space(6);

        // Nome do alias
        GUILayout.Label("Nome do Key Alias:", sLabel);
        keyAlias = EditorGUILayout.TextField(keyAlias);

        EditorGUILayout.Space(8);

        // Senha do alias
        GUILayout.Label("Senha do Key Alias (Key Password):", sLabel);
        GUILayout.BeginHorizontal();
        if (mostrarAliasPass)
            keyAliasPass = EditorGUILayout.TextField(keyAliasPass);
        else
            keyAliasPass = EditorGUILayout.PasswordField(keyAliasPass);

        if (GUILayout.Button(mostrarAliasPass ? "🙈" : "👁", GUILayout.Width(28), GUILayout.Height(18)))
            mostrarAliasPass = !mostrarAliasPass;
        GUILayout.EndHorizontal();

        DesenharForcaSenha(keyAliasPass);

        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  SEÇÃO AÇÕES
    // ════════════════════════════════════════════════════════
    void DesenharSecaoAcoes()
    {
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("AÇÕES", sTitulo);
        EditorGUILayout.Space(6);

        // Status de feedback
        if (!string.IsNullOrEmpty(mensagemStatus))
        {
            var stMsg = new GUIStyle(EditorStyles.boldLabel)
            {
                wordWrap = true,
                normal   = { textColor = corStatus }
            };
            GUILayout.Label(mensagemStatus, stMsg);
            EditorGUILayout.Space(4);
        }

        // Botão Salvar
        GUI.backgroundColor = cVerde;
        if (GUILayout.Button("💾  SALVAR CREDENCIAIS COM SEGURANÇA", sBotaoGrande))
            SalvarCredenciais();
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(6);

        GUILayout.BeginHorizontal();

        // Aplicar no PlayerSettings
        GUI.backgroundColor = cAzul;
        if (GUILayout.Button("⚙  Aplicar no\nPlayerSettings", GUILayout.Height(38)))
        {
            CarregarCredenciais();
            AplicarCredenciaisNoPlayerSettings(silencioso: false);
        }

        // Testar keystore
        GUI.backgroundColor = new Color(0.55f, 0.25f, 0.80f);
        if (GUILayout.Button("🔍  Verificar\nArquivo", GUILayout.Height(38)))
            TestarKeystore();

        // Abrir PlayerSettings
        GUI.backgroundColor = new Color(0.35f, 0.35f, 0.45f);
        if (GUILayout.Button("🛠  Abrir\nPlayer Settings", GUILayout.Height(38)))
            SettingsService.OpenProjectSettings("Project/Player");

        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        // Botão Apagar credenciais
        GUI.backgroundColor = cVermel;
        if (GUILayout.Button("🗑  Apagar Todas as Credenciais Salvas", GUILayout.Height(28)))
            ApagarCredenciais();
        GUI.backgroundColor = Color.white;

        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  SEÇÃO SEGURANÇA
    // ════════════════════════════════════════════════════════
    void DesenharSecaoSeguranca()
    {
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("🔒  COMO A SEGURANÇA FUNCIONA", sTitulo);
        EditorGUILayout.Space(4);

        var stInfo = new GUIStyle(EditorStyles.miniLabel)
        {
            wordWrap = true,
            normal   = { textColor = new Color(0.72f, 0.72f, 0.82f) }
        };

        bool isWindows = Application.platform == RuntimePlatform.WindowsEditor;

        GUILayout.Label(
            isWindows
                ? "🪟 Windows: As senhas são criptografadas com a DPAPI (Data Protection API " +
                  "do Windows). A chave é vinculada ao seu usuário e máquina — nenhum outro " +
                  "computador consegue descriptografar."
                : "🍎 Mac/Linux: As senhas são criptografadas com AES-256 usando um salt " +
                  "derivado do nome da máquina. Não é tão seguro quanto DPAPI, mas protege " +
                  "contra leitura direta do arquivo de preferências.",
            stInfo);

        EditorGUILayout.Space(4);

        GUILayout.Label(
            "✅ As senhas NÃO ficam em texto puro no EditorPrefs.\n" +
            "✅ EditorPrefs é local por máquina — nunca vai para o git.\n" +
            "✅ Cada projeto tem chaves separadas para evitar conflitos.\n" +
            "⚠ Não compartilhe o .keystore em repositórios públicos.",
            stInfo);

        GUILayout.EndVertical();
    }

    // ════════════════════════════════════════════════════════
    //  SEÇÃO GITIGNORE
    // ════════════════════════════════════════════════════════
    void DesenharSecaoGitIgnore()
    {
        GUILayout.BeginVertical(sSecao);
        GUILayout.Label("📁  PROTEÇÃO DO ARQUIVO .KEYSTORE", sTitulo);
        EditorGUILayout.Space(4);

        bool gitIgnoreOk = VerificarGitIgnore();

        if (gitIgnoreOk)
        {
            var stOk = new GUIStyle(EditorStyles.boldLabel)
            { normal = { textColor = cVerde }, fontSize = 10 };
            GUILayout.Label("✅ .gitignore protege arquivos *.keystore e *.jks", stOk);
        }
        else
        {
            var stAviso = new GUIStyle(EditorStyles.boldLabel)
            { normal = { textColor = cLaranja }, fontSize = 10 };
            GUILayout.Label("⚠  .gitignore não possui regra para *.keystore", stAviso);
            EditorGUILayout.Space(4);

            GUI.backgroundColor = cLaranja;
            if (GUILayout.Button("➕  Adicionar *.keystore e *.jks ao .gitignore", GUILayout.Height(28)))
                AdicionarAoGitIgnore();
            GUI.backgroundColor = Color.white;
        }

        GUILayout.EndVertical();
        EditorGUILayout.Space(6);
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — SALVAR CREDENCIAIS
    // ════════════════════════════════════════════════════════
    void SalvarCredenciais()
    {
        try
        {
            if (!string.IsNullOrEmpty(keystorePath))
                EditorPrefs.SetString(KeyPath, keystorePath);

            if (!string.IsNullOrEmpty(keystorePass))
                EditorPrefs.SetString(KeyKSPass, Cifrar(keystorePass));

            if (!string.IsNullOrEmpty(keyAlias))
                EditorPrefs.SetString(KeyAlias, keyAlias);

            if (!string.IsNullOrEmpty(keyAliasPass))
                EditorPrefs.SetString(KeyAliasPass, Cifrar(keyAliasPass));

            // Aplica imediatamente no PlayerSettings
            AplicarCredenciaisNoPlayerSettings(silencioso: true);

            SetStatus("✅ Credenciais salvas e aplicadas no PlayerSettings!", cVerde);
            Debug.LogWarning("[🔑 KeystoreManager] Credenciais salvas com segurança.");
        }
        catch (Exception e)
        {
            SetStatus($"❌ Erro ao salvar: {e.Message}", cVermel);
            Debug.LogWarning($"[🔑 KeystoreManager] Aviso ao salvar: {e.Message}");
        }
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — CARREGAR CREDENCIAIS
    // ════════════════════════════════════════════════════════
    void CarregarCredenciais()
    {
        try
        {
            keystorePath = EditorPrefs.GetString(KeyPath, "");
            keyAlias     = EditorPrefs.GetString(KeyAlias, "");

            string ksPassCifrada    = EditorPrefs.GetString(KeyKSPass, "");
            string aliasPassCifrada = EditorPrefs.GetString(KeyAliasPass, "");

            keystorePass  = string.IsNullOrEmpty(ksPassCifrada)    ? "" : Decifrar(ksPassCifrada);
            keyAliasPass  = string.IsNullOrEmpty(aliasPassCifrada)  ? "" : Decifrar(aliasPassCifrada);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[🔑 KeystoreManager] Aviso ao carregar credenciais: {e.Message}");
        }
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — APLICAR NO PLAYER SETTINGS
    // ════════════════════════════════════════════════════════
    public static void AplicarCredenciaisNoPlayerSettings(bool silencioso)
    {
        try
        {
            string path          = EditorPrefs.GetString(Prefixo2() + "KeystorePath",    "");
            string ksPassCifrada = EditorPrefs.GetString(Prefixo2() + "KeystorePass",    "");
            string alias         = EditorPrefs.GetString(Prefixo2() + "KeyAlias",        "");
            string alPassCifrada = EditorPrefs.GetString(Prefixo2() + "KeyAliasPass",    "");

            if (string.IsNullOrEmpty(ksPassCifrada) && string.IsNullOrEmpty(alPassCifrada))
            {
                if (!silencioso)
                    Debug.LogWarning("[🔑 KeystoreManager] Nenhuma credencial salva encontrada.");
                return;
            }

            string ksPass    = string.IsNullOrEmpty(ksPassCifrada) ? "" : Decifrar(ksPassCifrada);
            string aliasPass = string.IsNullOrEmpty(alPassCifrada) ? "" : Decifrar(alPassCifrada);

            if (!string.IsNullOrEmpty(path))
            {
                PlayerSettings.Android.keystoreName = path;
                PlayerSettings.Android.keystorePass = ksPass;
            }

            if (!string.IsNullOrEmpty(alias))
            {
                PlayerSettings.Android.keyaliasName = alias;
                PlayerSettings.Android.keyaliasPass = aliasPass;
            }

            if (!silencioso)
            {
                Debug.LogWarning(
                    "[🔑 KeystoreManager] ✅ Credenciais aplicadas no PlayerSettings.Android.");
                EditorUtility.DisplayDialog("Credenciais Aplicadas ✅",
                    "PlayerSettings.Android atualizado com sucesso!\n\n" +
                    $"Keystore: {(string.IsNullOrEmpty(path) ? "(não definido)" : Path.GetFileName(path))}\n" +
                    $"Alias: {(string.IsNullOrEmpty(alias) ? "(não definido)" : alias)}",
                    "OK");
            }
            else
            {
                // Auto-load silencioso ao abrir o editor
                Debug.LogWarning("[🔑 KeystoreManager] ⚡ Credenciais Android carregadas automaticamente.");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[🔑 KeystoreManager] Aviso ao aplicar credenciais: {e.Message}");
        }
    }

    static string Prefixo2() => $"KM_{PlayerSettings.productName}_";

    // ════════════════════════════════════════════════════════
    //  LÓGICA — APAGAR CREDENCIAIS
    // ════════════════════════════════════════════════════════
    void ApagarCredenciais()
    {
        bool confirmar = EditorUtility.DisplayDialog(
            "⚠ Apagar Credenciais",
            "Isso vai remover todas as credenciais do Keystore Manager " +
            "deste projeto do seu computador.\n\nDeseja continuar?",
            "Sim, apagar", "Cancelar");

        if (!confirmar) return;

        EditorPrefs.DeleteKey(KeyPath);
        EditorPrefs.DeleteKey(KeyKSPass);
        EditorPrefs.DeleteKey(KeyAlias);
        EditorPrefs.DeleteKey(KeyAliasPass);

        keystorePath = keystorePass = keyAlias = keyAliasPass = "";

        // Limpa o PlayerSettings também
        PlayerSettings.Android.keystoreName = "";
        PlayerSettings.Android.keystorePass = "";
        PlayerSettings.Android.keyaliasName = "";
        PlayerSettings.Android.keyaliasPass = "";

        SetStatus("🗑 Credenciais apagadas.", cLaranja);
        Debug.LogWarning("[🔑 KeystoreManager] Credenciais removidas do EditorPrefs.");
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — TESTAR KEYSTORE
    // ════════════════════════════════════════════════════════
    void TestarKeystore()
    {
        if (string.IsNullOrEmpty(keystorePath))
        {
            EditorUtility.DisplayDialog("Caminho vazio", "Informe o caminho do arquivo .keystore.", "OK");
            return;
        }

        if (!File.Exists(keystorePath))
        {
            SetStatus("❌ Arquivo .keystore não encontrado no caminho informado.", cVermel);
            EditorUtility.DisplayDialog("Arquivo não encontrado",
                $"O arquivo não existe:\n{keystorePath}", "OK");
            return;
        }

        FileInfo fi     = new FileInfo(keystorePath);
        bool    temPass = !string.IsNullOrEmpty(keystorePass);
        bool    temAlias = !string.IsNullOrEmpty(keyAlias);
        bool    temAliasPass = !string.IsNullOrEmpty(keyAliasPass);

        string resultado =
            $"✅ Arquivo encontrado!\n\n" +
            $"📁 Nome: {fi.Name}\n" +
            $"📦 Tamanho: {fi.Length} bytes\n" +
            $"📅 Modificado: {fi.LastWriteTime:dd/MM/yyyy HH:mm}\n\n" +
            $"🔑 Keystore Password: {(temPass     ? "✅ Definida"        : "⚠ Não definida")}\n" +
            $"🔖 Key Alias:         {(temAlias    ? $"✅ \"{keyAlias}\"" : "⚠ Não definido")}\n" +
            $"🔒 Alias Password:    {(temAliasPass? "✅ Definida"        : "⚠ Não definida")}";

        SetStatus("✅ Keystore verificado com sucesso.", cVerde);
        EditorUtility.DisplayDialog("🔍 Verificação do Keystore", resultado, "OK");
    }

    // ════════════════════════════════════════════════════════
    //  LÓGICA — GITIGNORE
    // ════════════════════════════════════════════════════════
    bool VerificarGitIgnore()
    {
        string gitIgnorePath = Path.Combine(
            Path.GetDirectoryName(Application.dataPath), ".gitignore");

        if (!File.Exists(gitIgnorePath)) return false;

        string conteudo = File.ReadAllText(gitIgnorePath);
        return conteudo.Contains("*.keystore") || conteudo.Contains("*.jks");
    }

    void AdicionarAoGitIgnore()
    {
        string gitIgnorePath = Path.Combine(
            Path.GetDirectoryName(Application.dataPath), ".gitignore");

        string linhas =
            "\n# Keystore — não commitar credenciais de assinatura\n" +
            "*.keystore\n" +
            "*.jks\n";

        if (File.Exists(gitIgnorePath))
            File.AppendAllText(gitIgnorePath, linhas);
        else
            File.WriteAllText(gitIgnorePath, linhas);

        Debug.LogWarning("[🔑 KeystoreManager] *.keystore e *.jks adicionados ao .gitignore.");
        EditorUtility.DisplayDialog("✅ .gitignore Atualizado",
            "As regras *.keystore e *.jks foram adicionadas ao .gitignore.\n\n" +
            "Seus arquivos de keystore não serão mais commitados no git.", "OK");
        Repaint();
    }

    // ════════════════════════════════════════════════════════
    //  CRIPTOGRAFIA
    // ════════════════════════════════════════════════════════
    static string Cifrar(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return "";
        byte[] dados = Encoding.UTF8.GetBytes(texto);

#if UNITY_EDITOR_WIN
        // Windows: DPAPI — criptografia vinculada ao usuário/máquina
        byte[] cifrado = ProtectedData.Protect(
            dados,
            ObterEntropy(),
            DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(cifrado);
#else
        // Mac/Linux: AES-256 com salt da máquina
        return CifrarAES(dados);
#endif
    }

    static string Decifrar(string textoCifrado)
    {
        if (string.IsNullOrEmpty(textoCifrado)) return "";
        byte[] dados = Convert.FromBase64String(textoCifrado);

#if UNITY_EDITOR_WIN
        byte[] decifrado = ProtectedData.Unprotect(
            dados,
            ObterEntropy(),
            DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(decifrado);
#else
        return DecifrarAES(dados);
#endif
    }

    // Entropy adicional para DPAPI (salt do projeto)
    static byte[] ObterEntropy()
    {
        string salt = $"UnityKM_{PlayerSettings.productName}_{SystemInfo.deviceUniqueIdentifier}";
        return Encoding.UTF8.GetBytes(salt);
    }

    // AES-256 para Mac/Linux
    static string CifrarAES(byte[] dados)
    {
        byte[] chave = DerivarChave();
        using (Aes aes = Aes.Create())
        {
            aes.Key = chave;
            aes.GenerateIV();
            using (var enc = aes.CreateEncryptor())
            {
                byte[] cifrado = enc.TransformFinalBlock(dados, 0, dados.Length);
                // Prefixo o IV no resultado
                byte[] resultado = new byte[aes.IV.Length + cifrado.Length];
                Buffer.BlockCopy(aes.IV, 0, resultado, 0, aes.IV.Length);
                Buffer.BlockCopy(cifrado, 0, resultado, aes.IV.Length, cifrado.Length);
                return Convert.ToBase64String(resultado);
            }
        }
    }

    static string DecifrarAES(byte[] dadosCombinados)
    {
        byte[] chave = DerivarChave();
        using (Aes aes = Aes.Create())
        {
            aes.Key = chave;
            byte[] iv      = new byte[16];
            byte[] cifrado = new byte[dadosCombinados.Length - 16];
            Buffer.BlockCopy(dadosCombinados, 0,  iv,      0, 16);
            Buffer.BlockCopy(dadosCombinados, 16, cifrado, 0, cifrado.Length);
            aes.IV = iv;
            using (var dec = aes.CreateDecryptor())
            {
                byte[] decifrado = dec.TransformFinalBlock(cifrado, 0, cifrado.Length);
                return Encoding.UTF8.GetString(decifrado);
            }
        }
    }

    static byte[] DerivarChave()
    {
        string saltStr = $"KM_{PlayerSettings.productName}_{SystemInfo.deviceUniqueIdentifier}";
        using (var deriv = new Rfc2898DeriveBytes(
            SystemInfo.deviceName,
            Encoding.UTF8.GetBytes(saltStr),
            10000))
        {
            return deriv.GetBytes(32); // 256 bits
        }
    }

    // ════════════════════════════════════════════════════════
    //  UI — FORÇA DE SENHA
    // ════════════════════════════════════════════════════════
    void DesenharForcaSenha(string senha)
    {
        if (string.IsNullOrEmpty(senha)) return;

        int forca = CalcularForca(senha);
        Color cor  = forca < 2 ? cVermel : forca < 4 ? cLaranja : cVerde;
        string txt = forca < 2 ? "Fraca" : forca < 4 ? "Média" : "Forte";

        GUILayout.BeginHorizontal();
        GUILayout.Label($"  Força: {txt}", new GUIStyle(EditorStyles.miniLabel)
        { normal = { textColor = cor } }, GUILayout.Width(90));

        Rect barraR = GUILayoutUtility.GetRect(0, 8, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(barraR, new Color(0.15f, 0.15f, 0.22f));
        float pct = forca / 5f;
        EditorGUI.DrawRect(new Rect(barraR.x, barraR.y, barraR.width * pct, barraR.height), cor);
        GUILayout.EndHorizontal();
        EditorGUILayout.Space(2);
    }

    int CalcularForca(string s)
    {
        int f = 0;
        if (s.Length >= 8)             f++;
        if (s.Length >= 12)            f++;
        if (System.Text.RegularExpressions.Regex.IsMatch(s, "[A-Z]")) f++;
        if (System.Text.RegularExpressions.Regex.IsMatch(s, "[0-9]")) f++;
        if (System.Text.RegularExpressions.Regex.IsMatch(s, "[^a-zA-Z0-9]")) f++;
        return f;
    }

    // ════════════════════════════════════════════════════════
    //  UTILITÁRIOS
    // ════════════════════════════════════════════════════════
    void SetStatus(string msg, Color cor)
    {
        mensagemStatus = msg;
        corStatus      = cor;
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
        sLabel  = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.72f, 0.72f, 0.82f) } };
        sBotaoGrande = new GUIStyle(GUI.skin.button)
        { fontSize = 11, fontStyle = FontStyle.Bold, fixedHeight = 38, normal = { textColor = Color.white } };
        sBotaoVerde  = new GUIStyle(GUI.skin.button) { fixedHeight = 30, fontSize = 10 };
        sBotaoAzul   = new GUIStyle(GUI.skin.button) { fixedHeight = 30, fontSize = 10 };
        sBotaoVermelho = new GUIStyle(GUI.skin.button) { fixedHeight = 28, fontSize = 10 };
        sLabelPerigo = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = cLaranja }, wordWrap = true };
        sBotaoSenha  = new GUIStyle(GUI.skin.button) { fontSize = 12, fixedHeight = 18, fixedWidth = 28 };

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
        GUILayout.Label("🔑  KEYSTORE MANAGER", st);
        Rect r = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(r, new Color(0.4f, 0.85f, 1f, 0.4f));
        EditorGUILayout.Space(6);
    }

    void OnEnable() => CarregarCredenciais();
}
