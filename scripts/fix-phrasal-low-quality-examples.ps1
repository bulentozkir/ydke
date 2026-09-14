$ErrorActionPreference = 'Stop'
Set-Location 'C:\gitrepo\ydke'

$targets = @(
  [pscustomobject]@{ Path='data/phrasalverbsen.js'; Lang='en' },
  [pscustomobject]@{ Path='data/phrasalverbsfr.js'; Lang='fr' },
  [pscustomobject]@{ Path='data/partikelverbde.js'; Lang='de' },
  [pscustomobject]@{ Path='data/phrasalverbses.js'; Lang='es' },
  [pscustomobject]@{ Path='data/phrasalverbssp.js'; Lang='es' },
  [pscustomobject]@{ Path='data/phrasalverbspt.js'; Lang='pt' },
  [pscustomobject]@{ Path='data/phrasalverbsnl.js'; Lang='nl' },
  [pscustomobject]@{ Path='data/phrasalverbsit.js'; Lang='it' }
)

function Normalize-Compare([string]$text) {
  if ($null -eq $text) { return '' }
  $value = $text.ToLowerInvariant().Trim()
  $value = [regex]::Replace($value, '\s+', ' ')
  $value = [regex]::Replace($value, '[\.;,:!?"''()\[\]{}]+', '')
  return $value.Trim()
}

function Lower-First([string]$text) {
  if ([string]::IsNullOrWhiteSpace($text)) { return $text }
  if ($text.Length -eq 1) { return $text.ToLowerInvariant() }
  return $text.Substring(0, 1).ToLowerInvariant() + $text.Substring(1)
}

function Short-Meaning([string]$text, [string]$lang) {
  if ([string]::IsNullOrWhiteSpace($text)) { return 'bu anlami anlatmak' }

  $value = $text.Trim()
  $value = [regex]::Replace($value, '\s+', ' ')
  $parts = $value -split '[\.;]'
  if ($parts.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace($parts[0])) {
    $value = $parts[0].Trim()
  }

  if ($lang -eq 'en' -and $value -match '^(?i)to\s+') {
    $value = ($value -replace '^(?i)to\s+', '').Trim()
  }

  $value = $value.Trim(' ', '"', '''')
  if ($value.Length -gt 96) {
    $value = $value.Substring(0, 96).Trim()
  }

  if ([string]::IsNullOrWhiteSpace($value)) { return 'bu anlami anlatmak' }
  return Lower-First $value
}

function Hash-Word([string]$word) {
  if ([string]::IsNullOrWhiteSpace($word)) { return 0 }
  $sum = 0
  foreach ($char in $word.ToCharArray()) {
    $sum += [int][char]$char
  }
  return [math]::Abs($sum)
}

function Split-Bilingual([string]$text) {
  $separator = $text.LastIndexOf(' - ')
  if ($separator -lt 0) {
    $single = $text.Trim()
    return @($single, $single)
  }

  $original = $text.Substring(0, $separator).Trim()
  $turkish = $text.Substring($separator + 3).Trim()

  if ([string]::IsNullOrWhiteSpace($original)) { $original = $text.Trim() }
  if ([string]::IsNullOrWhiteSpace($turkish)) { $turkish = $original }

  return @($original, $turkish)
}

function Escape-Js([string]$text) {
  if ($null -eq $text) { return '' }
  return ($text -replace '\\', '\\\\' -replace '"', '\\"')
}

function New-Example([string]$lang, [string]$word, [string]$definitionText) {
  $parts = Split-Bilingual $definitionText
  $originalMeaning = Short-Meaning $parts[0] $lang
  $turkishMeaning = Short-Meaning $parts[1] 'tr'
  $variant = (Hash-Word $word) % 4

  $originalSentence = switch ($lang) {
    'en' {
      switch ($variant) {
        0 { "People often use $word when they need to $originalMeaning." }
        1 { "In the office, we use $word to $originalMeaning." }
        2 { "You can use $word if you want to $originalMeaning." }
        default { "They used $word because they needed to $originalMeaning." }
      }
    }
    'fr' {
      switch ($variant) {
        0 { "On utilise souvent $word quand on veut $originalMeaning." }
        1 { "Au travail, on utilise $word pour $originalMeaning." }
        2 { "Tu peux utiliser $word si tu veux $originalMeaning." }
        default { "Ils ont utilise $word parce qu'ils devaient $originalMeaning." }
      }
    }
    'de' {
      switch ($variant) {
        0 { "Man verwendet oft $word, wenn man $originalMeaning moechte." }
        1 { "Bei der Arbeit nutzen wir $word, um $originalMeaning." }
        2 { "Du kannst $word verwenden, wenn du $originalMeaning willst." }
        default { "Sie haben $word benutzt, weil sie $originalMeaning mussten." }
      }
    }
    'es' {
      switch ($variant) {
        0 { "Se usa mucho $word cuando se quiere $originalMeaning." }
        1 { "En el trabajo usamos $word para $originalMeaning." }
        2 { "Puedes usar $word si quieres $originalMeaning." }
        default { "Ellos usaron $word porque necesitaban $originalMeaning." }
      }
    }
    'pt' {
      switch ($variant) {
        0 { "Usa-se muito $word quando se quer $originalMeaning." }
        1 { "No trabalho usamos $word para $originalMeaning." }
        2 { "Voce pode usar $word se quiser $originalMeaning." }
        default { "Eles usaram $word porque precisavam $originalMeaning." }
      }
    }
    'nl' {
      switch ($variant) {
        0 { "Men gebruikt vaak $word wanneer men $originalMeaning wil." }
        1 { "Op het werk gebruiken we $word om $originalMeaning." }
        2 { "Je kunt $word gebruiken als je $originalMeaning wilt." }
        default { "Zij gebruikten $word omdat ze $originalMeaning nodig hadden." }
      }
    }
    'it' {
      switch ($variant) {
        0 { "Si usa spesso $word quando si vuole $originalMeaning." }
        1 { "Al lavoro usiamo $word per $originalMeaning." }
        2 { "Puoi usare $word se vuoi $originalMeaning." }
        default { "Hanno usato $word perche dovevano $originalMeaning." }
      }
    }
    default {
      "People use $word when they need to $originalMeaning."
    }
  }

  $turkishSentence = switch ($variant) {
    0 { "Genelde $turkishMeaning gerektiginde $word ifadesi kullanilir." }
    1 { "Bu durumda $turkishMeaning amaciyla $word denir." }
    2 { "Insanlar $turkishMeaning istediginde $word kullanir." }
    default { "$turkishMeaning icin sikca $word ifadesi tercih edilir." }
  }

  return "$originalSentence - $turkishSentence"
}

function Is-LowQuality([string]$exampleText, [string]$definitionText) {
  if ([string]::IsNullOrWhiteSpace($exampleText)) { return $true }

  $markers = @(
    'In class, we practiced',
    'En classe, nous avons pratique',
    'En classe, nous avons pratiqu',
    'Im Unterricht haben wir',
    "Nell'inglese quotidiano si usa",
    'No example sentence available',
    "Aucune phrase d'exemple disponible",
    'Kein Beispielsatz',
    'Bu kelime için örnek cümle bulunamadı'
  )

  foreach ($marker in $markers) {
    if ($exampleText -like "*$marker*") { return $true }
  }

  if ((Normalize-Compare $exampleText) -eq (Normalize-Compare $definitionText)) {
    return $true
  }

  return $false
}

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$replacementSummary = @()

foreach ($target in $targets) {
  $path = Join-Path (Get-Location) $target.Path
  $rawText = [System.IO.File]::ReadAllText($path, $utf8NoBom)
  $newline = if ($rawText.Contains("`r`n")) { "`r`n" } else { "`n" }
  $lines = $rawText -split "`r?`n", -1

  $currentWord = ''
  $currentDefinition = ''
  $changed = 0

  for ($index = 0; $index -lt $lines.Length; $index++) {
    $line = $lines[$index]

    if ($line -match '^\s*word:\s*"(.*)",\s*$') {
      $currentWord = [regex]::Unescape($matches[1]).Trim()
      continue
    }

    if ($line -match '^\s*definition:\s*"(.*)",\s*$') {
      $currentDefinition = [regex]::Unescape($matches[1]).Trim()
      continue
    }

    if ($line -match '^\s*example:\s*"(.*)",\s*$') {
      $exampleValue = [regex]::Unescape($matches[1]).Trim()

      if (-not (Is-LowQuality $exampleValue $currentDefinition)) { continue }
      if ([string]::IsNullOrWhiteSpace($currentWord)) { continue }

      $indent = ([regex]::Match($line, '^\s*')).Value
      $newExample = New-Example $target.Lang $currentWord $currentDefinition
      $escaped = Escape-Js $newExample
      $lines[$index] = $indent + 'example: "' + $escaped + '",' 
      $changed++
    }
  }

  [System.IO.File]::WriteAllText($path, ([string]::Join($newline, $lines)), $utf8NoBom)
  $replacementSummary += [pscustomobject]@{ File = $target.Path; Replaced = $changed }
}

$remainingSummary = @()
foreach ($target in $targets) {
  $lines = Get-Content -Path $target.Path -Encoding UTF8
  $currentDefinition = ''
  $remaining = 0

  for ($index = 0; $index -lt $lines.Count; $index++) {
    $line = $lines[$index]

    if ($line -match '^\s*definition:\s*"(.*)",\s*$') {
      $currentDefinition = [regex]::Unescape($matches[1]).Trim()
      continue
    }

    if ($line -match '^\s*example:\s*"(.*)",\s*$') {
      $exampleValue = [regex]::Unescape($matches[1]).Trim()
      if (Is-LowQuality $exampleValue $currentDefinition) {
        $remaining++
      }
    }
  }

  $remainingSummary += [pscustomobject]@{ File = $target.Path; RemainingLowQuality = $remaining }
}

Write-Host '=== Replacement summary ==='
$replacementSummary | Format-Table -AutoSize
Write-Host '=== Remaining low-quality entries ==='
$remainingSummary | Format-Table -AutoSize
