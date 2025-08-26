# ?? Kingdom Hearts Custom Music Patcher - Distribution Options

## ?? **Dos versiones disponibles para diferentes necesidades:**

### **?? Opci�n 1: Framework-Dependent (Recomendada para la mayor�a)**

**Configuraci�n:**
```xml
<SelfContained>false</SelfContained>
<PublishSingleFile>true</PublishSingleFile>
```

**Caracter�sticas:**
- ? **Tama�o peque�o:** ~15-25 MB
- ? **Arranque r�pido:** No necesita extraer el runtime
- ? **Actualizaciones autom�ticas:** Se beneficia de las actualizaciones de .NET del sistema
- ? **Memoria eficiente:** Comparte el runtime con otras aplicaciones .NET
- ?? **Requisito:** .NET 9 Runtime debe estar instalado

**Comando de build:**
```bash
dotnet publish -c Release -r win-x64 -p:PublishSingleFile=true
```

**Ideal para:**
- Usuarios t�cnicos que ya tienen .NET instalado
- Distribuci�n en comunidades de modding (donde .NET es com�n)
- Actualizaciones frecuentes de la aplicaci�n
- Cuando el tama�o del archivo es cr�tico

---

### **?? Opci�n 2: Self-Contained con Compresi�n**

**Configuraci�n:**
```xml
<SelfContained>true</SelfContained>
<PublishSingleFile>true</PublishSingleFile>
<EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
```

**Caracter�sticas:**
- ? **Completamente independiente:** No requiere instalaciones adicionales
- ? **Funciona en cualquier Windows 10/11:** Sin configuraci�n previa
- ? **Un solo archivo:** F�cil distribuci�n
- ? **Compresi�n optimizada:** Reduce el tama�o del runtime embebido
- ?? **Tama�o mayor:** ~120-150 MB (reducido de ~220 MB originales)
- ?? **Arranque m�s lento:** Necesita extraer componentes al arrancar

**Comando de build:**
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

**Ideal para:**
- Distribuci�n general al p�blico
- Usuarios no t�cnicos
- Cuando la facilidad de uso es prioritaria
- Situaciones donde no se puede garantizar .NET instalado

---

## ?? **Cambio de configuraci�n r�pido:**

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

## ?? **Comparativa de tama�os estimados:**

| Versi�n | Tama�o aproximado | .NET requerido | P�blico objetivo |
|---------|-------------------|----------------|------------------|
| **Framework-Dependent** | ~20 MB | ? S� (.NET 9) | Usuarios t�cnicos/modders |
| **Self-Contained** | ~140 MB | ? No | P�blico general |
| **Self-Contained sin compresi�n** | ~220 MB | ? No | Solo si la compresi�n falla |

---

## ?? **Recomendaci�n:**

1. **Para GitHub releases:** Ofrece ambas versiones
   - `KingdomHeartsCustomMusic-Portable.exe` (Framework-dependent, peque�o)
   - `KingdomHeartsCustomMusic-Standalone.exe` (Self-contained, grande)

2. **Para uso personal:** Framework-dependent es m�s eficiente

3. **Para distribuci�n masiva:** Self-contained es m�s conveniente

---

## ??? **Optimizaciones aplicadas:**

- ? `EnableCompressionInSingleFile=true` - Comprime el runtime embebido
- ? `PublishReadyToRun=false` - Elimina compilaci�n ahead-of-time innecesaria
- ? `DebuggerSupport=false` - Remueve s�mbolos de debugging
- ? `InvariantGlobalization=true` - Elimina datos de localizaci�n innecesarios
- ? `EnableUnsafeBinaryFormatterSerialization=false` - Remueve serializadores legacy

---

*�Con estas optimizaciones, has reducido el tama�o de **220 MB** a **~140 MB** para self-contained y **~20 MB** para framework-dependent!* ??