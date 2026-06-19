# 🎮 PainelDesignPro — Alien City 33
### Kit de Automação e DevOps para Unity

---

## 🚀 Início Rápido

Abra o hub central com:
```
Ferramentas → 🎛 PainelDesignPro   ou   Ctrl + Shift + D
```

---

## 📁 Estrutura desta pasta (Assets/Editor/)

```
Assets/
└── Editor/
    ├── Docs/                          ← você está aqui
    │   ├── README.md                  ← este arquivo
    │   ├── SEGURANCA.md               ← como funciona a criptografia do Keystore
    │   └── FERRAMENTAS.md             ← referência completa de todas as ferramentas
    │
    ├── AlinhadorUI.cs                 ← Ctrl+Shift+A  — Alinhador Figma-style
    ├── AnchorSnap.cs                  ← Ctrl+Shift+N  — Snap âncoras + simulador resoluções
    ├── AssetQualityLinter.cs          ← Ctrl+Shift+I  — Inspetor de Assets Pro
    ├── ConverterParaTransform.cs      ←               — Converte UI → 2D World
    ├── KeystoreManager.cs             ← Ctrl+Shift+K  — Gerenciador de assinatura Android
    ├── OlhoDeAguia.cs                 ← Ctrl+Shift+O  — Localizador de objetos perdidos
    ├── OrganizadorHierarquia.cs       ←               — Organiza e padroniza a Hierarchy
    ├── PaletaCoresGlobal.cs           ←               — Paleta de cores e temas
    ├── PainelDesignPro.cs             ← Ctrl+Shift+D  — Hub central DevOps
    ├── SceneCleaner.cs                ← Ctrl+Shift+F  — Faxina + Tags + Layers
    ├── ValidadorPreBuild.cs           ← Ctrl+Shift+B  — Verifica projeto antes da build
    └── Vassoureiro.cs                 ← Ctrl+Shift+V  — Remove GameObjects vazios
```

> ⚠ **Importante:** Todos os arquivos `.cs` nesta pasta são **Editor-only**.
> A Unity os exclui automaticamente de qualquer build de produção.
> Eles não aumentam o tamanho do jogo final.

---

## ⌨ Atalhos Rápidos

| Teclas | Ferramenta |
|--------|-----------|
| `Ctrl+Shift+D` | 🎛 PainelDesignPro (Hub) |
| `Ctrl+Shift+K` | 🔑 Keystore Manager |
| `Ctrl+Shift+B` | 🚀 Validador Pré-Build |
| `Ctrl+Shift+F` | 🗂 Scene Cleaner |
| `Ctrl+Shift+V` | 🧹 Vassoureiro |
| `Ctrl+Shift+I` | 🔎 Asset Quality Linter |
| `Ctrl+Shift+O` | 🦅 Olho de Águia |
| `Ctrl+Shift+N` | ⚓ Anchor Snap |
| `Ctrl+Shift+A` | ⬛ Alinhador UI |

---

## 📖 Documentação Detalhada

| Documento | Conteúdo |
|-----------|---------|
| [SEGURANCA.md](SEGURANCA.md) | Como funciona DPAPI, AES-256, PBKDF2, fluxos de criptografia |
| [FERRAMENTAS.md](FERRAMENTAS.md) | Referência completa de cada ferramenta com tabelas e exemplos |

---

## 🔑 Configuração Inicial do Keystore (Android)

Se este é um novo computador ou você ainda não configurou:

1. Abra `Ctrl+Shift+K` (Keystore Manager)
2. Clique em 📂 e selecione seu arquivo `.keystore`
3. Digite a **Senha do Keystore**
4. Digite o **Key Alias** e sua senha
5. Clique em **💾 SALVAR CREDENCIAIS COM SEGURANÇA**
6. Pronto! A Unity preencherá automaticamente nas próximas sessões

> 🔒 As senhas são cifradas com DPAPI (Windows) ou AES-256 (Mac/Linux).
> Nunca ficam em texto puro. Nunca vão para o git.
> Ver [SEGURANCA.md](SEGURANCA.md) para detalhes.

---

## 🧹 Workflow de Faxina Antes de uma Build

Siga este checklist antes de gerar `.aab` para o Google Play:

```
1. Ctrl+Shift+O  → 🦅 Olho de Águia
   └── Verificar se há objetos perdidos ou invisíveis
   └── Corrigir ou deletar os encontrados

2. Ctrl+Shift+V  → 🧹 Vassoureiro
   └── Remover GameObjects vazios

3. Ctrl+Shift+F  → 🗂 Scene Cleaner
   └── Aba Faxina → "VARRER A CENA"
   └── Remover Missing Scripts

4. Ctrl+Shift+I  → 🔎 Asset Quality Linter
   └── Selecionar assets no Project
   └── Clicar em "🔍 DIAGNOSTICAR"
   └── Corrigir texturas com filtro errado

5. Ctrl+Shift+B  → 🚀 Validador Pré-Build
   └── Clicar em "🚀 VERIFICAR PROJETO AGORA"
   └── Corrigir todos os itens ❌

6. Ctrl+Shift+D  → 🎛 PainelDesignPro
   └── Aba 🔑 Assinatura → "🔍 VALIDAR KEYSTORE"
   └── Confirmar ✅ antes de compilar
   └── Aba 🚀 Build → "📦 GERAR .aab"
```

---

## ⚠ Política de Log

Todas as ferramentas seguem a mesma política:

```
✅  Debug.LogWarning  (🟡 amarelo) — sempre
❌  Debug.LogError    (🔴 vermelho) — nunca
```

Isso garante que:
- Nenhum falso positivo aparece no CI/CD
- O console permanece limpo de erros desnecessários
- O git não registra erros de ferramentas internas

---

*Alien City 33 — PainelDesignPro v1.0*
*Criado com Antigravity AI — 2026*
