# ?? Kingdom Hearts Custom Music Patcher - Distribution Options

## ?? **Dos versiones disponibles para diferentes necesidades:**

### **?? Opción 1: Framework-Dependent (Recomendada para la mayoría)**

**Configuración:**
```xml
<SelfContained>false</SelfContained>
<PublishSingleFile>true</PublishSingleFile>
```

**Características:**
- ? **Tamaño pequeño:** ~15-25 MB
- ? **Arranque rápido:** No necesita extraer el runtime
- ? **Actualizaciones automáticas:** Se beneficia de las actualizaciones de .NET del sistema
- ? **Memoria eficiente:** Comparte el runtime con otras aplicaciones .NET
- ?? **Requisito:** .NET 9 Runtime debe estar instalado

**Comando de build:**
```bash
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true
```

**Ideal para:**
- Usuarios técnicos que ya tienen .NET instalado
- Distribución en comunidades de modding (donde .NET es común)
- Actualizaciones frecuentes de la aplicación
- Cuando el tamaño del archivo es crítico

---

### **?? Opción 2: Self-Contained con Compresión**

**Configuración:**
```xml
<SelfContained>true</SelfContained>
<PublishSingleFile>true</PublishSingleFile>
<EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
```

**Características:**
- ? **Completamente independiente:** No requiere instalaciones adicionales
- ? **Funciona en cualquier Windows 10/11:** Sin configuración previa
- ? **Un solo archivo:** Fácil distribución
- ? **Compresión optimizada:** Reduce el tamaño del runtime embebido
- ?? **Tamaño mayor:** ~120-150 MB (reducido de ~220 MB originales)
- ?? **Arranque más lento:** Necesita extraer componentes al arrancar

**Comando de build:**
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

**Ideal para:**
- Distribución general al público
- Usuarios no técnicos
- Cuando la facilidad de uso es prioritaria
- Situaciones donde no se puede garantizar .NET instalado

---

## ?? **Cambio de configuración rápido:**

Para cambiar entre versiones, simplemente modifica en `KingdomHeartsCustomMusic.csproj`:

### Framework-Dependent:
```xml
<SelfContained>false</SelfContained>
<!-- Quitar EnableCompressionInSingleFile -->
```

### Self-Contained:
```xml
<SelfContained>true</SelfContained>
<EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
```

---

## ?? **Comparativa de tamaños estimados:**

| Versión | Tamaño aproximado | .NET requerido | Público objetivo |
|---------|-------------------|----------------|------------------|
| **Framework-Dependent** | ~20 MB | ? Sí (.NET 9) | Usuarios técnicos/modders |
| **Self-Contained** | ~140 MB | ? No | Público general |
| **Self-Contained sin compresión** | ~220 MB | ? No | Solo si la compresión falla |

---

## ?? **Recomendación:**

1. **Para GitHub releases:** Ofrece ambas versiones
   - `KingdomHeartsCustomMusic-Portable.exe` (Framework-dependent, pequeño)
   - `KingdomHeartsCustomMusic-Standalone.exe` (Self-contained, grande)

2. **Para uso personal:** Framework-dependent es más eficiente

3. **Para distribución masiva:** Self-contained es más conveniente

---

## ??? **Optimizaciones aplicadas:**

- ? `EnableCompressionInSingleFile=true` - Comprime el runtime embebido
- ? `PublishReadyToRun=false` - Elimina compilación ahead-of-time innecesaria
- ? `DebuggerSupport=false` - Remueve símbolos de debugging
- ? `InvariantGlobalization=true` - Elimina datos de localización innecesarios
- ? `EnableUnsafeBinaryFormatterSerialization=false` - Remueve serializadores legacy

---

*¡Con estas optimizaciones, has reducido el tamaño de **220 MB** a **~140 MB** para self-contained y **~20 MB** para framework-dependent!* ??