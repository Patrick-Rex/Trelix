# 使用示例

本文档提供常见使用场景的示例。命令中的 `roslyn-tool` 简写和 Bash 续行用法见 [工具路径](../SKILL.md#工具路径)；在 Trelix 使用实际项目/源码路径，并重新确认符号行列。

## 目录

- [场景 1: 查找方法调用链](#场景-1-查找方法调用链)
- [场景 2: 分析类型继承结构](#场景-2-分析类型继承结构)
- [场景 3: 查找未使用代码](#场景-3-查找未使用代码)
- [场景 4: 跨文件引用追踪](#场景-4-跨文件引用追踪)
- [场景 5: 查找接口实现](#场景-5-查找接口实现)
- [场景 6: 查找使用某命名空间的文件](#场景-6-查找使用某命名空间的文件)
- [场景 7: 只知道符号名称](#场景-7-只知道符号名称)

---

## 场景 1: 查找方法调用链

**需求**: 了解 `ProcessOrder` 方法内部调用了哪些方法，以及这些方法又调用了什么。

**步骤**:

```bash
# 1. 获取直接调用（深度 1）
roslyn-tool method-call-graph ./MyProject.csproj \
  --file src/OrderService.cs --line 25 --column 20 \
  --depth 1

# 2. 获取二级调用链（深度 2）
roslyn-tool method-call-graph ./MyProject.csproj \
  --file src/OrderService.cs --line 25 --column 20 \
  --depth 2

# 3. 扩大静态调用图范围（深度 3，不代表所有运行时调用）
roslyn-tool method-call-graph ./MyProject.csproj \
  --file src/OrderService.cs --line 25 --column 20 \
  --depth 3
```

**输出解读**:
- `isExternal: false` - 项目内部方法，可以继续追踪
- `isExternal: true` - 外部方法（如 .NET 库），无法继续追踪

---

## 场景 2: 分析类型继承结构

**需求**: 了解 `OrderService` 类的继承关系，包括基类、接口和派生类。

**步骤**:

```bash
# 获取类型层级
roslyn-tool type-hierarchy ./MyProject.csproj \
  --file src/OrderService.cs --line 5 --column 15
```

**输出包含**:
- `baseType` - 基类信息
- `interfaces` - 实现的接口列表
- `derivedTypes` - 派生类列表

**结合 find-derived-types 查找更多派生类**:
```bash
roslyn-tool find-derived-types ./MyProject.csproj \
  --file src/BaseService.cs --line 1 --column 1
```

---

## 场景 3: 查找未使用代码

**需求**: 找出项目中可能存在死代码的字段。

**步骤**:

```bash
# 1. 查找从未被引用的 private 字段
roslyn-tool unused-fields ./MyProject.csproj --format json

# 2. 查找从未被赋值的字段（可能是只读但未初始化）
roslyn-tool never-assigned-fields ./MyProject.csproj --format json

# 3. 输出到文件以便后续分析
roslyn-tool unused-fields ./MyProject.csproj \
  --format json -o ./analysis/unused-fields.json
```

**分析建议**:
- 未使用的 private 字段可能是死代码，可以考虑删除
- 未赋值的字段可能需要初始化或通过依赖注入设置

---

## 场景 4: 跨文件引用追踪

**需求**: 找出 `UserService` 类在项目中的所有使用位置。

**步骤**:

```bash
# 1. 获取符号信息确认位置
roslyn-tool symbol-info ./MyProject.csproj \
  --file src/UserService.cs --line 5 --column 15

# 2. 查找所有引用
roslyn-tool find-references ./MyProject.csproj \
  --file src/UserService.cs --line 5 --column 15

# 3. 对于类引用，可以结合 find-usings 查找 using 语句
roslyn-tool find-usings ./MyProject.csproj \
  --namespace MyProject.Services --type UserService
```

---

## 场景 5: 查找接口实现

**需求**: 找出 `IOrderProcessor` 接口的所有实现类。

**步骤**:

```bash
# 查找接口实现
roslyn-tool find-implementations ./MyProject.csproj \
  --file src/IOrderProcessor.cs --line 5 --column 20
```

**输出包含**:
- 所有直接实现该接口的类
- 每个实现的文件位置和行号

---

## 场景 6: 查找使用某命名空间的文件

**需求**: 找出项目中使用了 `System.Collections.Generic` 命名空间的所有文件。

**步骤**:

```bash
# 查找使用某命名空间的文件
roslyn-tool find-usings ./MyProject.csproj \
  --namespace "System.Collections.Generic"

# 查找使用特定类型的文件
roslyn-tool find-usings ./MyProject.csproj \
  --namespace "System.Collections.Generic" --type List

# 查找项目自定义命名空间的使用
roslyn-tool find-usings ./MyProject.csproj \
  --namespace "MyProject.Services"
```

**输出包含**:
- 使用该命名空间的文件列表
- 每个文件中的 using 语句
- 具体使用位置的行号和上下文代码

---

## 工作流示例

### 场景 7: 只知道符号名称

```bash
roslyn-tool find-symbols ./MyProject.csproj --name OrderService --exact --kind type
roslyn-tool find-symbols ./MySolution.slnx --name ProcessOrder --exact --kind method --limit 50
```

检查退出码、`workspaceIncomplete` 和 `truncated`，根据 `project`、`displayName`、`file` 选择声明，再将返回的 `file`、`line`、`column` 传给 `symbol-info` 或 `find-references`。重载方法和 partial 类型可能返回多个位置，不直接选第一个。可执行的 PowerShell 串联示例见 [SKILL.md](../SKILL.md#快速使用示例)。

### 完整的代码审查流程

```bash
# 1. 获取项目中的未使用字段
roslyn-tool unused-fields ./MyProject.csproj --format json

# 2. 对于可疑的方法，查看调用关系
roslyn-tool method-call-graph ./MyProject.csproj \
  --file src/MyClass.cs --line 10 --column 5 --depth 2

# 3. 查找方法的引用确认是否真的未使用
roslyn-tool find-references ./MyProject.csproj \
  --file src/MyClass.cs --line 10 --column 5

# 4. 对于类，查看继承关系
roslyn-tool type-hierarchy ./MyProject.csproj \
  --file src/MyClass.cs --line 5 --column 15
```

### 重构前的影响分析

```bash
# 1. 查找要重构的方法的所有引用
roslyn-tool find-references ./MyProject.csproj \
  --file src/OldService.cs --line 20 --column 10

# 2. 查看调用链了解依赖关系
roslyn-tool method-call-graph ./MyProject.csproj \
  --file src/OldService.cs --line 20 --column 10 --depth 3

# 3. 如果是接口，查看所有实现
roslyn-tool find-implementations ./MyProject.csproj \
  --file src/IService.cs --line 5 --column 15
```
