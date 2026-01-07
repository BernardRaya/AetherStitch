# AetherStitch - C#项目本地化工具

基于 Roslyn 的 C# 项目本地化工具，支持字符串提取、翻译管理和代码替换。

## 特性

### 翻译池模式（Translation Pool）
- **每个唯一字符串只翻译一次** - 自动合并代码中的重复字符串
- **多位置追踪** - 每个翻译单元记录所有使用位置
- **统一管理** - 一处修改，应用到所有引用位置
- **代码上下文分离** - 翻译内容与代码位置独立，符合标准翻译工具格式

###  增量更新
- **智能追踪** - 保留已翻译内容，只更新变化部分
- **自动去重** - 相同内容的字符串自动合并
- **变化检测** - 精确识别新增、更新和删除的字符串
- **上下文更新** - 自动更新字符串在代码中的所有引用位置

### Roslyn 代码分析
- 支持字符串字面量（`"text"`）
- 支持字符串插值（`$"text {var}"`）
- 保留占位符和格式化符号
- 自动过滤系统字符串

## 安装

```bash
git clone https://github.com/your-repo/AetherStitch.git
cd AetherStitch
dotnet build
```

## 使用方法

### 1. 提取字符串

```bash
# 首次提取
aetherstitch extract --source "C:\MyProject" --output "mapping.json"

# 结果：
# ✅ 从代码中提取所有字符串
# ✅ 自动合并重复字符串
# ✅ translatedText 默认为原文（便于翻译人员修改）
```

### 2. 翻译字符串

编辑 `mapping.json` 文件，修改 `target` 字段：

```json
{
  "key": "welcome_message",
  "source": "Welcome to my app",
  "target": "欢迎使用我的应用",  // 修改此字段
  "contexts": [
    { "filePath": "Program.cs", "lineNumber": 10 },
    { "filePath": "UI/MainWindow.cs", "lineNumber": 25 }
  ]
}
```

### 3. 增量更新

```bash
# 代码更新后，保留已有翻译并更新变化部分
aetherstitch extract --source "C:\MyProject" --output "mapping.json" --update

# 结果：
# ✅ 保留已翻译内容
# ✅ 添加新字符串
# ✅ 更新变化的字符串
# ✅ 标记删除的字符串
```

### 4. 应用翻译

```bash
# 生成翻译后的项目
aetherstitch replace --source "C:\MyProject" --mapping "mapping.json" --output "C:\MyProject_CN"

# 结果：
# ✅ 复制项目到输出目录
# ✅ 应用所有翻译到代码
# ✅ 生成可直接编译的本地化项目

# 试运行模式（不实际修改文件）
aetherstitch replace --source "C:\MyProject" --mapping "mapping.json" --output "C:\MyProject_CN" --dry-run
```

### 5. 验证翻译

```bash
# 验证占位符、完整性等
aetherstitch validate --mapping "mapping.json" --strict

# 查看统计信息
aetherstitch stats --mapping "mapping.json"
```

## JSON 格式说明

### 翻译池结构（Version 2.0+）

```json
{
  "projectName": "MyProject",
  "sourceLanguage": "en-US",
  "targetLanguage": "zh-CN",
  "version": "2.0.0",
  "translations": [
    {
      "key": "a3f5e9c2...",
      "source": "Welcome to AetherStitch",
      "target": "欢迎使用 AetherStitch",
      "type": "Literal",
      "placeholders": [],
      "status": "Translated",
      "contexts": [
        {
          "filePath": "Program.cs",
          "lineNumber": 10,
          "codeContext": "Program.Main",
          "note": "Application startup message"
        },
        {
          "filePath": "UI/Welcome.cs",
          "lineNumber": 25,
          "codeContext": "WelcomeScreen.Show",
          "note": null
        }
      ]
    },
    {
      "key": "b7d2a1e8...",
      "source": "Hello {0}!",
      "target": "你好，{0}！",
      "type": "Interpolation",
      "placeholders": [
        {
          "index": 0,
          "expression": "userName",
          "placeholderToken": "{0}"
        }
      ],
      "status": "Translated",
      "contexts": [
        {
          "filePath": "Services/UserService.cs",
          "lineNumber": 45,
          "codeContext": "UserService.Greet"
        }
      ]
    }
  ],
  "metadata": {
    "totalTranslations": 100,      // 唯一翻译数
    "translatedCount": 50,          // 已翻译数
    "pendingCount": 50,             // 待翻译数
    "totalContexts": 250,           // 总出现次数
    "fileStatistics": {
      "Program.cs": 10,
      "Services/UserService.cs": 25
    }
  }
}
```

### 关键概念

- **Translation（翻译单元）**：每个唯一字符串对应一个翻译
- **Context（上下文引用）**：记录字符串在代码中的使用位置
- **Key**：基于字符串内容的唯一标识符
- **Source**：源语言文本
- **Target**：目标语言文本

### 优势对比

#### 传统模式（Version 1.0）
```json
// 相同字符串重复存储翻译
{
  "entries": [
    { "id": "1", "originalText": "Welcome", "translatedText": "欢迎", "filePath": "A.cs" },
    { "id": "2", "originalText": "Welcome", "translatedText": "欢迎", "filePath": "B.cs" },  // 重复
    { "id": "3", "originalText": "Welcome", "translatedText": "欢迎", "filePath": "C.cs" }   // 重复
  ]
}
```

#### 翻译池模式（Version 2.0）
```json
// 每个字符串只翻译一次
{
  "translations": [
    {
      "key": "welcome",
      "source": "Welcome",
      "target": "欢迎",  // 只翻译一次
      "contexts": [
        { "filePath": "A.cs", "lineNumber": 10 },
        { "filePath": "B.cs", "lineNumber": 20 },
        { "filePath": "C.cs", "lineNumber": 30 }
      ]
    }
  ]
}
```

**节省翻译工作量：** 如果一个字符串在代码中出现 10 次，只需翻译 1 次！

## CLI 命令参考

### extract - 提取字符串

```bash
aetherstitch extract --source <path> --output <file> [options]

选项：
  -s, --source <path>     源项目路径（.csproj 或目录）[必需]
  -o, --output <file>     输出文件路径 [默认: localization-mapping.json]
  --target <lang>         目标语言代码 [默认: zh-CN]
  --exclude <patterns>    排除的文件模式
  --update                更新现有 mapping（增量模式）
  --keep-deleted          保留已删除的字符串
```

### validate - 验证 Mapping

```bash
aetherstitch validate --mapping <file> [options]

选项：
  -m, --mapping <file>    Mapping 文件路径 [必需]
  --strict                严格模式（所有条目必须已翻译）
```

### replace - 应用翻译

```bash
aetherstitch replace --source <path> --mapping <file> --output <path> [options]

选项：
  -s, --source <path>     源项目路径 [必需]
  -m, --mapping <file>    Mapping 文件路径 [必需]
  -o, --output <path>     输出项目路径 [必需]
  --validate              替换前验证 mapping [默认: true]
  --overwrite             覆盖已存在的输出目录 [默认: false]
  --dry-run               试运行模式（不修改文件）[默认: false]
```

### stats - 显示统计

```bash
aetherstitch stats --mapping <file>

选项：
  -m, --mapping <file>    Mapping 文件路径 [必需]
```

## 工作流程示例

```bash
# 步骤 1: 初始提取
aetherstitch extract -s "C:\MyProject" -o "mapping.json"
# 输出: 163 unique translations (174 contexts)
#      ↑ 唯一字符串数    ↑ 代码中出现次数

# 步骤 2: 翻译
# 编辑 mapping.json，修改 target 字段

# 步骤 3: 验证
aetherstitch validate -m "mapping.json"

# 步骤 4: 查看进度
aetherstitch stats -m "mapping.json"
# Unique Translations: 163
# Translated: 50 (30.7%)
# Pending: 113 (69.3%)

# 步骤 5: 应用翻译生成本地化项目
aetherstitch replace -s "C:\MyProject" -m "mapping.json" -o "C:\MyProject_CN"
# === Replacement Summary ===
# Files modified: 15
# Total replacements: 50
# Localized project created: C:\MyProject_CN

# 步骤 6: 代码更新后增量更新
aetherstitch extract -s "C:\MyProject" -o "mapping.json" --update
# === Update Summary ===
# Unchanged: 160  (保留翻译)
# Updated: 2      (重新翻译)
# Added: 5        (新字符串)
# Deleted: 3      (已删除)
```

## 技术细节

### 字符串插值处理

**原代码：**
```csharp
$"User {userName} logged in at {DateTime.Now:yyyy-MM-dd}"
```

**提取为模板：**
```json
{
  "source": "User {0} logged in at {1}",
  "placeholders": [
    { "index": 0, "expression": "userName" },
    { "index": 1, "expression": "DateTime.Now:yyyy-MM-dd" }
  ]
}
```

**翻译：**
```json
{
  "target": "用户 {0} 在 {1} 登录"
}
```

### ID 生成策略

```csharp
// 基于内容生成唯一 Key（而不是位置）
Key = SHA256(Type + ":" + Source).Substring(0, 16)

// 示例：
"Literal:Welcome" → "a3f5e9c284d2e1b7"
"Interpolation:Hello {0}!" → "b7d2a1e8f3c4d5a6"
```

## 待实现功能

- [ ] update逻辑优化
- [ ] 过滤逻辑优化
- [ ] 导出crowdin格式
- [ ] 术语管理
