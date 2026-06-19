# 🎛 PainelDesignPro — Referência de Ferramentas
### Kit completo de automação para Unity — Alien City 33

---

## 📦 Ferramentas Disponíveis

| Atalho | Menu | Arquivo |
|--------|------|---------|
| `Ctrl+Shift+D` | 🎛 PainelDesignPro | `PainelDesignPro.cs` |
| `Ctrl+Shift+K` | 🔑 Keystore Manager | `KeystoreManager.cs` |
| `Ctrl+Shift+B` | 🚀 Validador Pré-Build | `ValidadorPreBuild.cs` |
| `Ctrl+Shift+F` | 🗂 Scene Cleaner | `SceneCleaner.cs` |
| `Ctrl+Shift+V` | 🧹 Vassoureiro | `Vassoureiro.cs` |
| `Ctrl+Shift+I` | 🔎 Asset Quality Linter | `AssetQualityLinter.cs` |
| `Ctrl+Shift+O` | 🦅 Olho de Águia | `OlhoDeAguia.cs` |
| `Ctrl+Shift+N` | ⚓ Anchor Snap | `AnchorSnap.cs` |
| `Ctrl+Shift+A` | ⬛ Alinhador UI | `AlinhadorUI.cs` |
| `Ctrl+Shift+C` | 🎨 Paleta de Cores | `PaletaCoresGlobal.cs` |
| `Ctrl+Shift+H` | 📐 Organizador Hierarquia | `OrganizadorHierarquia.cs` |

---

## 🎛 PainelDesignPro (Hub Central)

**Atalho:** `Ctrl+Shift+D`

Hub principal que centraliza todas as ferramentas em 4 abas:

| Aba | Conteúdo |
|-----|----------|
| 🔑 Assinatura | Validação de keystore, status das credenciais, injeção no PlayerSettings |
| 🚀 Build | Checklist pré-build, gerar .aab (Play) e .apk (teste) |
| 🔗 Ferramentas | 12 cartões de acesso rápido para todo o kit |
| 📊 Status | Info do app, cenas na build, configuração rápida IL2CPP/ARM64 |

---

## 🔑 Keystore Manager

**Atalho:** `Ctrl+Shift+K`

Gerencia as credenciais Android com segurança:

- Salva senha com **DPAPI** (Windows) ou **AES-256** (Mac/Linux)
- **[InitializeOnLoad]** — preenche `PlayerSettings.Android` automaticamente ao abrir o editor
- Indicador de **força de senha** (Fraca / Média / Forte)
- Botão 📂 para navegar e selecionar o arquivo `.keystore`
- Detecta e adiciona `*.keystore` ao `.gitignore`
- Apagar todas as credenciais salvas com 1 clique

> 📖 Veja `SEGURANCA.md` para detalhes da criptografia.

---

## 🚀 Validador Pré-Build

**Atalho:** `Ctrl+Shift+B`

Roda 6 verificações antes de compilar:

| Check | O que verifica | Auto-Fix |
|-------|---------------|:--------:|
| ✈ Cenas | Cenas ausentes/desabilitadas no Build Settings | Abre Build Settings |
| 🧩 Missing Refs | Campos nulos em prefabs via SerializedObject | Abre prefab |
| 📦 Missing Scripts | Scripts quebrados em prefabs | ✅ Remove |
| 💀 Missing Scripts | Scripts quebrados na cena ativa | ✅ Remove c/ Ctrl+Z |
| 🖼 Texturas | Tamanho acima do limite da plataforma | ✅ Ajusta via Importer |
| 🔊 Áudio | AudioClips sem compressão (PCM) | ✅ Converte para Vorbis |

**Plataformas configuráveis:** Mobile (1024px) | Desktop (4096px) | Console (2048px) | WebGL (2048px)

---

## 🗂 Scene Cleaner (Etiquetas & Faxina)

**Atalho:** `Ctrl+Shift+F`

3 abas integradas:

**🏷 Taguear:**
- Aplica Tag ou Layer em toda a seleção + filhos recursivos
- Atalhos rápidos: UI, Player, Enemy, Untagged, MainCamera, GameController
- Cria novas tags sem abrir Project Settings

**🧹 Faxina:**
- Remove GameObjects vazios (apenas Transform, sem filhos)
- Remove Missing Scripts (`GameObjectUtility.RemoveMonoBehavioursWithMissingScript`)
- Botão **Faxina Completa** — resolve tudo de uma vez

**📊 Relatório:**
- Barras visuais de distribuição por Tag e Layer
- Conta inativos, problemas e componentes principais

---

## 🧹 Vassoureiro (GameObjects Fantasmas)

**Atalho:** `Ctrl+Shift+V`

Especializado em encontrar e remover GameObjects completamente vazios:

- **Critério:** apenas Transform + sem filhos + sem nenhum componente
- Preview da lista **antes** de deletar qualquer coisa
- Botão `ping` em cada item para piscar na Hierarchy
- Filtros: na raiz, aninhados, por nome
- Suporte a **Ctrl+Z** (grupo de undo)
- Somente `LogWarning` — nunca erro vermelho

---

## 🔎 Asset Quality Linter (Inspetor de Assets)

**Atalho:** `Ctrl+Shift+I`
**Contexto:** Clique direito no Project → 🔎 Asset Quality Linter

Dois modos com botões lado a lado:

```
[ 🔍 DIAGNOSTICAR (Apenas Avisar) ] [ 🔧 AUTO-FIX (Corrigir Tudo) ]
```

| Check | Problema | Fix Automático |
|-------|---------|:-------------:|
| 🖼 Pixel Art | Filtro Bilinear, compressão, mipmaps | ✅ Point + Uncompressed |
| 📝 Nomes | Espaços, acentos, caracteres especiais | ✅ Renomeia para snake_case |
| 🗑 Materiais | Não usados em cenas/prefabs | ✅ Move para _Lixeira |
| 🔊 Áudio | Formato PCM sem compressão | ⚠ Avisa |
| 📋 Duplicados | Mesmo nome + mesmo tamanho | ⚠ Avisa |

Logs **clicáveis no Console** — clique abre o asset no Project.

---

## 🦅 Olho de Águia (Objetos Perdidos)

**Atalho:** `Ctrl+Shift+O`

Localiza objetos invisíveis, perdidos e fantasmas:

| Categoria | Severidade | O que detecta |
|-----------|:----------:|--------------|
| ⚖ Escala Zero | 🔴 | `Scale (0,0,0)` ou eixo zerado |
| 📍 Coordenadas | 🔴 | Além do limiar (padrão: 5000 unidades) |
| 🔢 NaN/Infinity | 🔴 | Posição matematicamente inválida |
| 👻 CanvasGroup | 🔴 | Alpha = 0 com scripts ativos |
| 🖱 Bloqueio UI | 🔴 | Image invisível com Raycast Target = true |
| 🎨 Sprite Alpha | 🟡 | `SpriteRenderer.color.a = 0` |
| 📦 Renderer Off | 🟡 | Renderer desativado, GO ativo |
| 🖼 UI Fora | 🟡 | `anchoredPosition` além de ±5000px |

Cada item: `[ Selecionar ] [ 📷 Focar Cena ] [ 🔧 Fix ] [ 🗑 Del ]`

---

## ⚓ Anchor Snap & Resoluções

**Atalho:** `Ctrl+Shift+N`

4 abas:
- **⚓ Âncoras:** Snap to Corners (1 objeto, filhos, cena inteira) + preview visual
- **📐 Presets:** Grade 4×4 com mini-preview desenhado de cada preset
- **📱 Resoluções:** Testa 11 resoluções na Game View (iPhone, Android, 4K, UltraWide...)
- **🔍 Diagnóstico:** Detecta âncoras "soltas" + checklist de responsividade

---

## ⬛ Alinhador & Distribuidor UI

**Atalho:** `Ctrl+Shift+A`

Inspirado no Figma/Canva:
- **Alinhar:** Esq, Centro H, Dir, Topo, Centro V, Base
- **Distribuir:** Espaçamento igualitário ou gap fixo em px
- **Igualar tamanho:** Largura, Altura ou ambos (referência = 1º selecionado)
- **Nudge pixel-perfect:** ◀ ▲ ▼ ▶ com passo configurável
- Funciona com **RectTransform (UI)** e **Transform (2D)**

---

## 🎨 Paleta de Cores Global

Gerencia e aplica temas de cores em toda a cena:
- 5 slots de cor: Primária, Secundária, Destaque, Fundo, Texto
- Aplica em todos os `Image` e `TextMeshProUGUI` com tag específica
- Verificação de contraste **WCAG 2.1** (acessibilidade)
- Salva/carrega paletas como favoritos

---

## 📐 Organizador de Hierarquia

Organiza e padroniza a Hierarchy:
- Agrupa objetos selecionados em pasta vazia
- Renomeação em lote com prefixo/sufixo
- Ordena filhos alfabeticamente
- Cria separadores visuais (`--- UI ---`, `--- Background ---`)
- Limpeza de objetos vazios integrada

---

## 🔄 Converter RectTransform → Transform

Converte elementos de UI para objetos 2D normais (fora do Canvas):
- Remove `CanvasRenderer`
- Converte `Image` → `SpriteRenderer`
- Reseta posição para `(0,0,0)` e Scale para `1`
- Modo bulk: converte todos os selecionados ou toda a seleção

---

## 📋 Política de Log (todas as ferramentas)

```
✅ SOMENTE Debug.LogWarning (🟡 amarelo no Console)
❌ NUNCA Debug.LogError   (🔴 vermelho)
```

**Por quê:** Erros vermelhos podem falsamente ativar alertas em CI/CD pipelines,
falhar scripts de automação de build, e poluir o console com mensagens que
parecem bugs críticos quando são apenas avisos de ferramenta.

---

*Projeto: Alien City 33*
*Pasta: `Assets/Editor/`*
*Documentação: `Assets/Editor/Docs/`*
