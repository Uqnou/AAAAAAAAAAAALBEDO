# Unity Test Harness via CLI

How to write and run tests for Unity projects using the Unity CLI. The
Antigravity agent uses these patterns to verify code changes.

## Test framework options

Unity supports two test frameworks:

1. **Unity Test Framework (UTF)** — built-in, uses NUnit attributes
2. **Custom test scripts** — simple C# scripts that exercise behavior

For the dev cycle, custom test scripts are often simpler and more flexible.

## Running existing tests

If the project has Unity Test Framework tests:

```bash
# Run all tests
unity command run_tests

# Run tests matching a filter
unity command run_tests --filter "MyTestClass"

# Run tests in a specific mode
unity command run_tests --mode EditMode
unity command run_tests --mode PlayMode
```

## Writing custom test scripts

For projects without a test framework, the Antigravity agent can write a simple
test script that exercises the target behavior:

```csharp
// Assets/Tests/MyFeatureTest.cs
using UnityEngine;

public class MyFeatureTest : MonoBehaviour
{
    public static bool RunTests()
    {
        int passed = 0;
        int failed = 0;

        // Test 1: Basic functionality
        try
        {
            var result = MyFeature.DoSomething();
            if (result == expectedValue)
            {
                Debug.Log("PASS: Test 1");
                passed++;
            }
            else
            {
                Debug.Log("FAIL: Test 1 — expected " + expectedValue + ", got " + result);
                failed++;
            }
        }
        catch (System.Exception e)
        {
            Debug.Log("FAIL: Test 1 — exception: " + e.Message);
            failed++;
        }

        // Test 2: Edge case
        // ...

        Debug.Log($"Results: {passed} passed, {failed} failed");
        return failed == 0;
    }
}
```

Then run it via:

```bash
unity command eval "return MyFeatureTest.RunTests();"
```

## Test patterns

### Compile test

The simplest test — does the code compile?

```bash
unity command eval "return true;"
```

If this returns `true`, the code compiles. If it returns an error, there are
compile errors.

### Unit test

Test a specific method or class:

```bash
unity command eval "return MyClass.MyMethod(5) == 10;"
```

### Integration test

Test that components work together:

```bash
unity command eval @"
    var go = new GameObject();
    var comp = go.AddComponent<MyComponent>();
    comp.DoSomething();
    return comp.State == expectedState;
"
```

### Scene test

Test that a scene loads correctly:

```bash
unity command eval @"
    UnityEngine.SceneManagement.SceneManager.LoadScene("MyScene");
    return true;
"
```

## Test output

The Antigravity agent must capture and report:

1. The exact command that was run
2. The full output (including any errors)
3. Pass/fail counts
4. Any stack traces

Example output to report:

```
$ unity command eval "return MyFeatureTest.RunTests();"
PASS: Test 1
PASS: Test 2
FAIL: Test 3 — expected 42, got 0
Results: 2 passed, 1 failed
```

## Best practices

- **Always compile first.** Run `unity command eval "return true;"` before
  running tests to catch compile errors early.
- **Test one thing at a time.** Each test should verify a single behavior.
- **Use descriptive names.** Test names should describe what they verify.
- **Report all output.** Don't summarize — report the exact output so Hermes can
  verify.
- **Clean up.** If tests create GameObjects or modify state, clean up after
  themselves.

## Play mode tests

For tests that require Play mode (e.g., testing Update() methods):

```bash
unity command run_tests --mode PlayMode
```

Or via eval:

```bash
unity command eval @"
    // Enter play mode
    UnityEditor.EditorApplication.isPlaying = true;
    // Wait a frame
    yield return null;
    // Test
    var result = GameObject.Find("Player").GetComponent<PlayerController>().health;
    return result == 100;
"
```

Note: Play mode tests are slower and more complex. Use them only when necessary.
