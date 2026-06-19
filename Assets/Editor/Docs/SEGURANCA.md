# 🛡 Segurança — Keystore Manager
### Como as senhas são protegidas no projeto Unity

---

## 📋 Visão Geral do Fluxo

```
┌─────────────────────────────────────────────────────────────────┐
│                    SALVAR (uma vez)                             │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Você digita a senha                                            │
│         │                                                       │
│         ▼                                                       │
│   ┌─────────────┐                                               │
│   │  Cifrar()   │  ← DPAPI (Windows) ou AES-256 (Mac/Linux)    │
│   └──────┬──────┘                                               │
│          │                                                       │
│          ▼                                                       │
│   ┌──────────────────────────────────────────────┐             │
│   │  EditorPrefs  (registro do Windows / plist)  │             │
│   │  Chave: "KM_AlienCity33_KeystorePass"        │             │
│   │  Valor: "a9kX3mP+cQz7Yw..." (cifrado)        │             │
│   └──────────────────────────────────────────────┘             │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                CARREGAR (automático ao abrir o editor)          │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  [InitializeOnLoad]  ──► editor inicializa                      │
│         │                                                       │
│         ▼                                                       │
│   EditorPrefs.GetString("KM_..._KeystorePass")                  │
│         │                                                       │
│         ▼                                                       │
│   ┌──────────────┐                                              │
│   │  Decifrar()  │  ← Mesma chave criptográfica da máquina      │
│   └──────┬───────┘                                              │
│          │                                                       │
│          ▼                                                       │
│   PlayerSettings.Android.keystorePass = "sua_senha"            │
│   PlayerSettings.Android.keyaliasPass = "sua_senha"            │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## 🪟 Windows — DPAPI (Data Protection API)

**O que é:**
A DPAPI é uma API nativa do sistema operacional Windows, disponível via
`System.Security.Cryptography.ProtectedData`. Ela cifra os dados usando
uma chave **derivada automaticamente do perfil do usuário Windows + máquina**.

**Por que é segura:**
- A chave de criptografia **nunca é armazenada** — ela é gerada na hora
  a partir do contexto do usuário logado no Windows
- Nenhum outro computador consegue decifrar os dados
- Nenhuma outra conta de usuário no mesmo PC consegue decifrar
- Não existe arquivo de chave que possa vazar

**Implementação no KeystoreManager.cs:**
```csharp
// CIFRAR (Windows)
byte[] cifrado = ProtectedData.Protect(
    dados,              // senha em bytes
    ObterEntropy(),     // salt extra: nome do projeto + deviceUniqueIdentifier
    DataProtectionScope.CurrentUser   // vinculado ao usuário atual
);

// DECIFRAR (Windows)
byte[] original = ProtectedData.Unprotect(
    dados,
    ObterEntropy(),
    DataProtectionScope.CurrentUser
);
```

**O que é o "Entropy" (salt extra):**
```csharp
static byte[] ObterEntropy()
{
    // Combina nome do projeto + ID único da máquina
    // Garante que keystores de projetos diferentes não conflitem
    string salt = $"UnityKM_{PlayerSettings.productName}_{SystemInfo.deviceUniqueIdentifier}";
    return Encoding.UTF8.GetBytes(salt);
}
```

**Onde fica armazenado no Windows:**
```
HKEY_CURRENT_USER\SOFTWARE\Unity Technologies\Unity Editor 5.x\
  └── KM_AlienCity33_KeystorePass_hXXXX   = [dados cifrados em Base64]
  └── KM_AlienCity33_KeyAliasPass_hXXXX   = [dados cifrados em Base64]
```
*(mesmo que alguém leia o registro, verá apenas Base64 ilegível)*

---

## 🍎 Mac / Linux — AES-256 com PBKDF2

**Por que AES no Mac/Linux:**
O Mac/Linux não possui DPAPI. Usamos AES-256 (Advanced Encryption Standard,
256 bits) com uma chave derivada do hardware da máquina via PBKDF2.

**Implementação:**
```csharp
static byte[] DerivarChave()
{
    // Rfc2898DeriveBytes = PBKDF2 (Password-Based Key Derivation Function 2)
    // 10.000 iterações tornam ataques de força bruta impraticáveis
    using (var derivador = new Rfc2898DeriveBytes(
        SystemInfo.deviceName,                           // "senha" = nome da máquina
        Encoding.UTF8.GetBytes(                          // salt =
            $"KM_{PlayerSettings.productName}_" +        //   prefixo do projeto +
            SystemInfo.deviceUniqueIdentifier),          //   ID único do hardware
        10000))                                          // 10.000 iterações PBKDF2
    {
        return derivador.GetBytes(32); // → 256 bits de chave AES
    }
}
```

**Fluxo completo Mac/Linux:**
```
                  ┌─────────────────────────┐
DeviceName   ──►  │                         │
ProjectName  ──►  │  PBKDF2 (10.000 iter.)  │ ──► Chave AES-256 (32 bytes)
DeviceUID    ──►  │                         │
                  └─────────────────────────┘
                              │
                              ▼
                  ┌─────────────────────────┐
Senha        ──►  │  AES-256 + IV aleatório │ ──► [IV (16 bytes) + Cifrado]
                  └─────────────────────────┘
                              │
                              ▼
                     Base64 → EditorPrefs
```

**IV (Initialization Vector):**
A cada salvamento, um IV aleatório é gerado e prefixado ao resultado.
Isso garante que a mesma senha produz resultados cifrados diferentes
a cada salvamento, impedindo análise de padrões.

```csharp
aes.GenerateIV(); // IV novo a cada vez
// Resultado final: [16 bytes de IV] + [dados cifrados]
// Ao decifrar: extrai o IV dos primeiros 16 bytes e usa para decifrar o resto
```

---

## 🔒 Por que EditorPrefs não vai para o git?

| Característica | Detalhe |
|---|---|
| **Local por máquina** | EditorPrefs são armazenados no sistema operacional, não em arquivos do projeto |
| **Fora do Assets/** | Ficam no registro (Windows) ou plist (Mac) — fora de qualquer pasta versionada |
| **Não aparecem no git status** | Nunca entram em `.gitignore` porque nunca estariam no repositório |
| **Por usuário** | Cada dev tem seus próprios EditorPrefs — não conflitam |

**Windows:** `HKEY_CURRENT_USER\SOFTWARE\Unity Technologies\...`
**Mac:** `~/Library/Preferences/com.unity3d.UnityEditor5.x.plist`
**Linux:** `~/.config/unity3d/...`

---

## 🔑 Nomenclatura das Chaves no EditorPrefs

Para evitar colisão entre projetos diferentes abertos no mesmo computador,
cada chave inclui o nome do produto:

```
KM_{ProductName}_{Campo}

Exemplos para "Alien City 33":
  KM_AlienCity33_KeystorePath      → caminho do arquivo .keystore
  KM_AlienCity33_KeystorePass      → senha cifrada do keystore
  KM_AlienCity33_KeyAlias          → nome do alias (não precisa cifragem)
  KM_AlienCity33_KeyAliasPass      → senha cifrada do alias
```

---

## ⚠️ O que a ferramenta NÃO protege

| Limitação | Explicação |
|---|---|
| Acesso físico à máquina | DPAPI protege por usuário — acesso físico com a mesma conta pode expor |
| Malware com nível de usuário | Um keylogger ou malware com mesmos privilégios pode ler memória |
| Mac sem senha de login | Sem senha de conta, qualquer um com acesso físico pode derivar a chave |

**Recomendação:** Mantenha o arquivo `.keystore` em local seguro e nunca
o commite no repositório. A ferramenta garante o `.gitignore` automaticamente.

---

## ✅ Resumo das Garantias

```
✅ Senha nunca em texto puro no EditorPrefs
✅ Chave criptográfica vinculada ao hardware + usuário
✅ Nunca vai para o git (EditorPrefs é local por máquina)
✅ Cada projeto tem chaves separadas (sem conflito)
✅ Preenche PlayerSettings.Android automaticamente ao abrir Unity
✅ DPAPI (Windows) = sem arquivo de chave que possa vazar
✅ AES-256 + PBKDF2 10k iterações (Mac/Linux) = força bruta impraticável
⚠ LogWarning apenas — nenhum erro vermelho gerado por esta ferramenta
```

---

*Gerado automaticamente pelo PainelDesignPro — Alien City 33*
*Arquivos relacionados: `Assets/Editor/KeystoreManager.cs` | `Assets/Editor/PainelDesignPro.cs`*
