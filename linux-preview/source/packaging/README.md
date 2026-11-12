# Building FooMeter for Windows (0.10.29)

Build the existing AscendAPP.slnx solution in Release. The desktop output is FooMeter.exe / FooMeter.dll, requiring .NET 10 Windows Desktop Runtime x64 and Npcap. Product branding is FooMeter; namespaces and the shared profile directory remain compatible with AscendAPP.

Build-Installer.ps1 and AscendAPP.iss describe the optional self-contained Inno Setup build. No installer was produced or tested for this release. The delivered Windows ZIP is framework dependent.

See the root README and docs/LOOT_COUNTER_0.10.29.md.
