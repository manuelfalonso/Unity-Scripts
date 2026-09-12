# Code Style Sheet

The authoritative C# style sheet for SombraStudios.Shared, adapted from the
[Unity Code Style Guide e-book](https://unity.com/resources/create-code-style-guide-e-book).

It describes *style*: naming, formatting, comments, events. Architecture rules —
POCOs over MonoBehaviours, composition, optional-package guards — live in
`CLAUDE.md` instead.

## General

- Microsoft's [Framework Design Guidelines](https://docs.microsoft.com/en-us/dotnet/standard/design-guidelines/).
- Google also maintains a [C# style guide](https://google.github.io/styleguide/csharp-style.html).

## Naming and casing

- Use Pascal case (`ExamplePlayerController`, `MaxHealth`) unless noted otherwise.
- Use camel case (`examplePlayerController`, `maxHealth`) for local and private variables, and parameters.
- Avoid snake case (`snake_case`), kebab case (`kebab-case`) and Hungarian notation (`strHungarianNotation`).
- If a file contains a MonoBehaviour, the file name must match the type name.

## Formatting

- Allman braces — opening brace on its own line.
- Keep lines short. Standard line width is 120 characters.
- A single space before flow-control conditions: `while (x == y)`.
- No spaces inside brackets: `x = dataArray[index]`.
- A single space after a comma between arguments.
- No space between the parenthesis and the arguments: `CollectItem(myObject, 0, 1);`.
- No space between a method name and its parenthesis: `DropPowerUp(myPrefab, 0, 1);`.
- Use vertical spacing — an extra blank line — for visual separation.

## Comments

- Rather than answering "what" or "how", comments fill in the gaps and tell us **why**.
- Use `//` to keep the explanation next to the logic.
- One space between the comment delimiter and the text.
- Begin with an uppercase letter and end with a period.
- Remove commented-out code.
- Add `TODO` comments only if you are really going to complete them.
- Use a `[Tooltip]` instead of a comment for serialized fields.
- Use a `[Header]` to group serialized fields.
- Avoid regions. They encourage large class sizes, and collapsed code is harder to read.
- Link to an external reference for legal or licensing information to save space.
- Use a `<summary>` XML tag in front of public methods for documentation and IntelliSense.

## Using lines

- Keep using lines at the top of the file.
- Remove unused lines.

```csharp
using System.Collections.Generic;
using UnityEngine;
using System;
```

## Namespaces

- Pascal case, without special symbols or underscores.
- Add a using line at the top to avoid typing the namespace repeatedly.
- Create sub-namespaces with the dot operator: `MyApplication.GameFlow`, `MyApplication.AI`.

```csharp
namespace StyleSheet
{
}
```

## Enums

- Use a singular type name.
- No prefix or suffix.

```csharp
public enum Direction
{
    North,
    South,
    East,
    West,
}
```

## Flags enums

- Use a plural type name.
- No prefix or suffix.
- Use column alignment for binary values.

```csharp
[Flags]
public enum AttackModes
{
    // Decimal                         // Binary
    None = 0,                          // 000000
    Melee = 1,                         // 000001
    Ranged = 2,                        // 000010
    Special = 4,                       // 000100

    MeleeAndSpecial = Melee | Special  // 000101
}
```

## Interfaces

- Name interfaces with adjective phrases.
- Use the `I` prefix.

```csharp
public interface IDamageable
{
    string DamageTypeName { get; }
    float DamageValue { get; }

    // Methods start with a verb or verb phrase to show an action.
    // Parameter names are camelCase.
    bool ApplyDamage(string description, float damage, int numberOfHits);
}

public interface IDamageable<T>
{
    void Damage(T damageTaken);
}
```

## ScriptableObjects

- Name ScriptableObjects with the suffix `SO`.
- Use Pascal case.
- Use the `CreateAssetMenu` attribute to create a menu item in the Unity Editor.
- Use `Sombra Studios` as the first part of the `menuName`.
- `fileName` without spaces.

```csharp
[CreateAssetMenu(fileName = "NewItemData", menuName = "Sombra Studios/Item Data")]
public class TestSO : ScriptableObject
{
    [SerializeField] private string _name;
    [SerializeField, Multiline] private string _description;
    [SerializeField] private float _rarity = 0f;

    public float Rarity => _rarity;
    public string Name => _name;
    public string Description => _description;
}
```

## Classes and structs

- Name them with nouns or noun phrases.
- Avoid prefixes.
- One MonoBehaviour per file, and the file name must match the type name.

```csharp
public class CodeStyle : MonoBehaviour
{
}
```

## Fields

- Avoid special characters (backslashes, symbols, Unicode); these can interfere with command line tools.
- Use nouns for names, but prefix booleans with a verb.
- Use meaningful names. Make names searchable and pronounceable. Don't abbreviate, unless it's math.
- Pascal case for public fields, camel case for private variables.
- Add an underscore in front of private fields to differentiate them from local variables.
- Alternatively use more explicit prefixes: `m_` = member variable, `s_` = static, `k_` = const.
- Specify the default access modifier.

```csharp
private int _elapsedTimeInDays;

// Use [SerializeField] if you want to display a private field in the Inspector.
// Booleans ask a question that can be answered true or false.
[SerializeField] private bool _isPlayerDead;

// This groups data from the custom PlayerStats class in the Inspector.
[SerializeField] private PlayerStats _stats;

// This limits the values to a Range and creates a slider in the Inspector.
[Range(0f, 1f)][SerializeField] private float _rangedStat;

// A tooltip can replace a comment on a serialized field and do double duty.
[Tooltip("This is another statistic for the player.")]
[SerializeField] private float _anotherStat;
```

## Properties

- Preferable to a public field.
- Pascal case, without special characters.
- Use expression-bodied properties to shorten, but choose your preferred format — for example,
  expression-bodied for read-only properties and `{ get; set; }` for everything else.
- Use an auto-implemented property for a public property without a backing field.

```csharp
// The private backing field.
private int _maxHealth;

// Read-only, returns the backing field.
public int MaxHealthReadOnly => _maxHealth;

// Equivalent to:
// public int MaxHealth { get; private set; }

// Explicitly implementing getter and setter.
public int MaxHealth
{
    get => _maxHealth;
    set => _maxHealth = value;
}

// Write-only (not using a backing field).
public int Health { private get; set; }

// Write-only, without an explicit setter.
public void SetMaxHealth(int newMaxValue) => _maxHealth = newMaxValue;

// Auto-implemented property without a backing field.
public string DescriptionName { get; set; } = "Fireball";
```

## Events

- Name with a verb phrase.
- Present participle means "before", past participle means "after".
- Use the `System.Action` delegate for most events; it takes 0 to 16 parameters.
- Define a custom `EventArg` only if necessary, either inherited from `System.EventArgs` or a custom struct.
- Naming scheme: event/action `DoorOpened`, event raising method `OnDoorOpened`,
  event handling method `MySubject_DoorOpened`.

```csharp
// Event before.
public event Action OpeningDoor;

// Event after.
public event Action DoorOpened;

public event Action<int> PointsScored;
public event Action<CustomEventArgs> ThingHappened;

// These are event raising methods, e.g. OnDoorOpened, OnPointsScored.
public void OnDoorOpened()
{
    DoorOpened?.Invoke();
}

public void OnPointsScored(int points)
{
    PointsScored?.Invoke(points);
}

// This is a custom EventArg made from a struct.
public struct CustomEventArgs
{
    public int ObjectID { get; }
    public Color Color { get; }

    public CustomEventArgs(int objectId, Color color)
    {
        this.ObjectID = objectId;
        this.Color = color;
    }
}
```

## Methods

- Start a method name with a verb or verb phrase to show an action.
- Parameter names are camel case.

```csharp
// Methods start with a verb.
public void SetInitialPosition(float x, float y, float z)
{
    transform.position = new Vector3(x, y, z);
}

// Methods ask a question when they return bool.
public bool IsNewPosition(Vector3 newPosition)
{
    return (transform.position == newPosition);
}
```

## var

- Use `var` if it helps readability, especially with long type names.
- Avoid `var` if it makes the type ambiguous.

```csharp
var powerUps = new List<PlayerStats>();
var dict = new Dictionary<string, List<GameObject>>();
```

## Switch statements

- Indent each case and the break underneath.

```csharp
switch (someExpression)
{
    case 0:
        // ..
        break;
    case 1:
        // ..
        break;
    case 2:
        // ..
        break;
}
```

## Braces

- Avoid single-line statements entirely, for debuggability.
- Keep braces in nested multi-line statements.

```csharp
// You can set a breakpoint on the clause.
for (int i = 0; i < 100; i++)
{
    DoSomething(i);
}

// Don't remove the braces here.
for (int i = 0; i < 10; i++)
{
    for (int j = 0; j < 10; j++)
    {
        DoSomething(j);
    }
}
```

## Other classes

- Define as many other helper or non-MonoBehaviour classes in your file as needed.
- This is a serializable struct that groups fields in the Inspector.

```csharp
[Serializable]
public struct PlayerStats
{
    public int MovementSpeed;
    public int HitPoints;
    public bool HasHealthPotion;
}
```
