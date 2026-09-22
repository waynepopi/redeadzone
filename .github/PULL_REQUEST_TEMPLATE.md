## Description

<!-- Briefly describe what this PR does and why. -->

## Related issue

<!-- Closes #<issue number>, or "N/A" -->

## Checklist

- [ ] `dotnet restore` succeeds
- [ ] `dotnet build Deadzone.sln -c Release` succeeds with zero errors
- [ ] `dotnet test Deadzone.sln -c Release` passes (all tests green)
- [ ] No unrelated architectural changes (WPF, SDL3, HIDMaestro not replaced)
- [ ] No dependency version changes unless explained below
- [ ] Documentation updated if user-visible behaviour changed
- [ ] Hardware testing identified (controller model, connection method, Windows version) if relevant

## Hardware tested (if applicable)

<!-- Controller model, USB/Bluetooth, Windows version — or "N/A" -->

## Dependency changes

<!-- If you changed any package versions, explain why here — or "None" -->
