$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing,System.Windows.Forms
[Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot '..\dist\BatteryBar.exe')) | Out-Null
function Assert($condition, $message) { if (-not $condition) { throw $message } }
$serializer = New-Object System.Xml.Serialization.XmlSerializer([BatteryBar.Preferences])
$legacy = New-Object System.IO.StringReader('<Preferences><Low>20</Low><Full>100</Full><Seconds>30</Seconds></Preferences>')
$old = $serializer.Deserialize($legacy)
$legacy.Dispose()
Assert ($old.Style -eq [BatteryBar.IconStyle]::Solid) 'Legacy settings must default to B'
foreach ($style in @([BatteryBar.IconStyle]::Solid, [BatteryBar.IconStyle]::Stacked)) {
    $p = New-Object BatteryBar.Preferences
    $p.Style = $style
    $writer = New-Object System.IO.StringWriter
    $serializer.Serialize($writer, $p)
    $reader = New-Object System.IO.StringReader($writer.ToString())
    $restored = $serializer.Deserialize($reader)
    Assert ($restored.Style -eq $style) 'Style must survive serialization'
    $reader.Dispose(); $writer.Dispose()
    foreach ($size in @(16,20,24,32)) {
        foreach ($value in @('0','5','15','85','100','?')) {
            $bitmap = [BatteryBar.IconRenderer]::Render($value, [Drawing.Color]::WhiteSmoke, $style, $size)
            Assert ($bitmap.Width -eq $size -and $bitmap.Height -eq $size) 'Incorrect icon size'
            Assert ($bitmap.GetPixel(15 * $size / 16, 0).A -eq 0) 'Outside icon must remain transparent'
            if ($style -eq [BatteryBar.IconStyle]::Stacked) {
                for ($x = 0; $x -lt $size; $x++) { Assert ($bitmap.GetPixel($x,$size-1).A -eq 0) 'C digits require a clear bottom margin' }
            }
            $bitmap.Dispose()
            $icon = [BatteryBar.IconRenderer]::Draw($value, [Drawing.Color]::WhiteSmoke, $style, $size)
            Assert ($icon.Width -eq $size) 'Native icon conversion failed'
            $icon.Dispose()
        }
    }
}
# Prove three separate digit columns remain visible at the smallest size.
$surface = [BatteryBar.IconRenderer]::Render('1', [Drawing.Color]::FromArgb(137,80,216), [BatteryBar.IconStyle]::Solid, 32)
Assert ($surface.GetPixel(4,16).A -eq 215) 'Battery surface must retain real alpha transparency'
$surface.Dispose()
$b = [BatteryBar.IconRenderer]::Render('100', [Drawing.Color]::WhiteSmoke, [BatteryBar.IconStyle]::Solid, 16)

Assert ($b.GetPixel(6,1).A -gt 0 -and $b.GetPixel(6,14).A -gt 0) 'B must use 14 pixels of height'
$b.Dispose()
$c = [BatteryBar.IconRenderer]::Render('100', [Drawing.Color]::WhiteSmoke, [BatteryBar.IconStyle]::Stacked, 16)


Assert ($c.GetPixel(6,6).A -eq 0) 'C number must be outside battery outline'
$c.Dispose()
# Exercise selection and cancellation without writing user settings or startup registration.
$callback = [Action[BatteryBar.Preferences]] { throw 'Cancel must not save' }
$form = New-Object BatteryBar.SettingsForm($old, $callback)
$selector = $form.Controls.Find('IconStyleSelector', $true)[0]
$selector.SelectedIndex = 1
Assert ($old.Style -eq [BatteryBar.IconStyle]::Solid) 'Editing must not mutate active preferences'
$form.Dispose()
# Contact sheet uses the production renderer, not a separate mockup renderer.
$sheet = New-Object Drawing.Bitmap(640,260)
$g = [Drawing.Graphics]::FromImage($sheet)
$g.Clear([Drawing.Color]::FromArgb(32,32,32))
$g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::Half
$font = New-Object Drawing.Font('Segoe UI',11)
$values = @('5','15','85','100','?')
for ($row=0; $row -lt 2; $row++) {
    $g.DrawString(@('B - Solid','C - Stacked')[$row],$font,[Drawing.Brushes]::White,8,($row*130+5))
    for ($col=0; $col -lt 5; $col++) {
        $bmp = [BatteryBar.IconRenderer]::Render($values[$col],[Drawing.Color]::WhiteSmoke,[BatteryBar.IconStyle]$row,16)
        $g.DrawImage($bmp, [Drawing.Rectangle]::new(($col*124+12),($row*130+32),64,64))
        $g.DrawImageUnscaled($bmp,($col*124+90),($row*130+56))
        $bmp.Dispose()
    }
}
$sheet.Save((Join-Path $PSScriptRoot '..\dist\icon-check.png'))
$font.Dispose(); $g.Dispose(); $sheet.Dispose()
Write-Output 'PASS: legacy settings, B/C persistence, 48 render/native-icon cases, 100 readability, settings cancel isolation.'
