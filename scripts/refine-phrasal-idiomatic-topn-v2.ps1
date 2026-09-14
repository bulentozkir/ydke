param(
  [int]$TopN = 300
)

$ErrorActionPreference = 'Stop'
Set-Location 'C:\gitrepo\ydke'

$targets = @(
  [pscustomobject]@{ Path = 'data/phrasalverbssp.js'; Lang = 'es' },
  [pscustomobject]@{ Path = 'data/phrasalverbspt.js'; Lang = 'pt' },
  [pscustomobject]@{ Path = 'data/phrasalverbsnl.js'; Lang = 'nl' },
  [pscustomobject]@{ Path = 'data/phrasalverbsit.js'; Lang = 'it' }
)

$localTemplates = @{
  es = @(
    "En la reunion de hoy, tuvimos que {0} para desbloquear el proyecto.",
    "Con el plazo encima, me toco {0} sin perder la calma.",
    "Cuando el cliente cambio de idea, decidimos {0} de inmediato.",
    "Para evitar otra queja, el equipo intento {0} desde el principio.",
    "Durante la llamada, ella logro {0} y cerrar el tema.",
    "Ayer en la oficina, nos pidieron {0} antes del mediodia.",
    "Si queremos terminar hoy, conviene {0} ahora mismo.",
    "Aunque habia presion, el gerente prefirio {0} paso a paso.",
    "En esta situacion, lo mas util fue {0}.",
    "Despues del incidente, aprendimos a {0} mejor.",
    "Al final del dia, aun faltaba {0}.",
    "Cada semana intentamos {0} para evitar errores repetidos."
  )
  pt = @(
    "Na reuniao de hoje, tivemos de {0} para destravar o projeto.",
    "Com o prazo apertado, tive de {0} sem perder a calma.",
    "Quando o cliente mudou de ideia, decidimos {0} na hora.",
    "Para evitar nova reclamacao, a equipa tentou {0} desde o inicio.",
    "Durante a chamada, ela conseguiu {0} e fechar o assunto.",
    "Ontem no escritorio, pediram-nos para {0} antes do meio-dia.",
    "Se queremos terminar hoje, convem {0} agora.",
    "Mesmo com pressao, o gerente preferiu {0} passo a passo.",
    "Nesta situacao, o mais util foi {0}.",
    "Depois do incidente, aprendemos a {0} melhor.",
    "No fim do dia, ainda faltava {0}.",
    "Toda semana tentamos {0} para evitar erros repetidos."
  )
  nl = @(
    "In de vergadering van vandaag stond dit centraal: {0}.",
    "Met de deadline dichtbij draaide het vooral om: {0}.",
    "Toen de klant van koers veranderde, lag de nadruk op: {0}.",
    "Om nieuwe klachten te voorkomen focuste het team op: {0}.",
    "Tijdens het gesprek werkte zij stap voor stap aan: {0}.",
    "Gisteren op kantoor kregen we als prioriteit: {0}.",
    "Als we vandaag willen afronden, moeten we letten op: {0}.",
    "Ondanks de druk hield de manager vast aan: {0}.",
    "In deze situatie bleek vooral belangrijk: {0}.",
    "Na het incident leerden we beter omgaan met: {0}.",
    "Aan het eind van de dag ontbrak nog: {0}.",
    "Elke week werken we verder aan: {0}."
  )
  it = @(
    "Nella riunione di oggi abbiamo dovuto {0} per sbloccare il progetto.",
    "Con la scadenza vicina, ho dovuto {0} senza perdere la calma.",
    "Quando il cliente ha cambiato idea, abbiamo deciso di {0} subito.",
    "Per evitare un altro reclamo, il team ha cercato di {0} fin dall'inizio.",
    "Durante la chiamata, lei e riuscita a {0} e chiudere la questione.",
    "Ieri in ufficio ci hanno chiesto di {0} prima di mezzogiorno.",
    "Se vogliamo chiudere oggi, conviene {0} adesso.",
    "Anche sotto pressione, il manager ha preferito {0} passo dopo passo.",
    "In questa situazione, la scelta piu utile e stata {0}.",
    "Dopo l'incidente abbiamo imparato a {0} meglio.",
    "A fine giornata restava ancora da {0}.",
    "Ogni settimana proviamo a {0} per evitare errori ripetuti."
  )
}

$trTemplates = @(
  "Bugunku toplantida odak noktamiz su oldu: {0}.",
  "Son tarih yakinken ekip su adima yogunlasti: {0}.",
  "Musteri fikrini degistirince hemen suya yoneldik: {0}.",
  "Yeni bir sikayeti onlemek icin en basta sunu ele aldik: {0}.",
  "Gorusmede konuyu kapatirken one cikan adim suydu: {0}.",
  "Dun ofiste oglene kadar onceligimiz suydu: {0}.",
  "Bugun bitirmek icin once su gerekiyordu: {0}.",
  "Baski altinda yonetici adim adim sunu secti: {0}.",
  "Bu durumda en ise yarayan yaklasim suydu: {0}.",
  "Olaydan sonra daha iyi ogrendigimiz sey suydu: {0}.",
  "Gun sonunda hala tamamlamamiz gereken sey suydu: {0}.",
  "Her hafta tekrar eden hatalari onlemek icin suya calisiyoruz: {0}."
)

function Escape-Js([string]$text) {
  if ($null -eq $text) { return '' }
  return ($text -replace '\\', '\\\\' -replace '"', '\\"')
}

function Lower-First([string]$text) {
  if ([string]::IsNullOrWhiteSpace($text)) { return $text }
  if ($text.Length -eq 1) { return $text.ToLowerInvariant() }
  return $text.Substring(0, 1).ToLowerInvariant() + $text.Substring(1)
}

function Clean-Meaning([string]$text) {
  if ([string]::IsNullOrWhiteSpace($text)) { return '' }
  $x = [regex]::Unescape($text).Trim()
  $x = [regex]::Replace($x, '\s+', ' ')
  $x = $x.Trim(' ', '.', ',', ';', ':', '-', '"', '''')

  # Keep only the first major segment to avoid overloaded definitions.
  $segments = $x -split '[\.;]'
  if ($segments.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace($segments[0])) {
    $x = $segments[0].Trim()
  }

  # Drop leading "to" in EN-style infinitives when present.
  $x = ($x -replace '^(?i)to\s+', '').Trim()

  return Lower-First $x
}

function Split-Definition([string]$definition) {
  if ([string]::IsNullOrWhiteSpace($definition)) {
    return @('', '')
  }

  $idx = $definition.LastIndexOf(' - ')
  if ($idx -lt 0) {
    $single = Clean-Meaning $definition
    return @($single, $single)
  }

  $left = Clean-Meaning ($definition.Substring(0, $idx))
  $right = Clean-Meaning ($definition.Substring($idx + 3))

  if ([string]::IsNullOrWhiteSpace($left)) { $left = $right }
  if ([string]::IsNullOrWhiteSpace($right)) { $right = $left }

  return @($left, $right)
}

function New-IdiomaticExample([string]$lang, [string]$word, [string]$definitionText, [int]$entryIndex) {
  $parts = Split-Definition $definitionText
  $localMeaning = $parts[0]
  $trMeaning = $parts[1]

  if ($lang -eq 'it' -and $localMeaning -match '^significato comune del phrasal verb\s+(?<w>.+)$') {
    $localMeaning = "capire il significato comune del phrasal verb $($matches['w'])"
  }
  if ($trMeaning -match '^(?<w>.+) ogek fiilinin yaygin anlami$') {
    $trMeaning = "$($matches['w']) ogek fiilinin yaygin anlamini kavramak"
  }

  if ([string]::IsNullOrWhiteSpace($localMeaning)) { $localMeaning = "usar '$word' con mas naturalidad" }
  if ([string]::IsNullOrWhiteSpace($trMeaning)) { $trMeaning = "'$word' ifadesini dogal kullanmak" }

  $localSet = $localTemplates[$lang]
  $seed = [math]::Abs(($word.GetHashCode() * 37) + ($entryIndex * 101))
  $idx = $seed % $localSet.Count
  $trIdx = $seed % $trTemplates.Count

  $left = [string]::Format($localSet[$idx], $localMeaning)
  $right = [string]::Format($trTemplates[$trIdx], $trMeaning)
  return "$left - $right"
}

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$summary = @()

foreach ($target in $targets) {
  $path = Join-Path (Get-Location) $target.Path
  $rawText = [System.IO.File]::ReadAllText($path, $utf8NoBom)
  $newline = if ($rawText.Contains("`r`n")) { "`r`n" } else { "`n" }
  $lines = $rawText -split "`r?`n", -1

  $entryIndex = 0
  $currentWord = ''
  $currentDefinition = ''
  $rewritten = 0

  for ($lineIndex = 0; $lineIndex -lt $lines.Length; $lineIndex++) {
    $line = $lines[$lineIndex]

    if ($line -match '^\s*word:\s*"(.*)",\s*$') {
      $entryIndex++
      $currentWord = [regex]::Unescape($matches[1]).Trim()
      continue
    }

    if ($line -match '^\s*definition:\s*"(.*)",\s*$') {
      $currentDefinition = [regex]::Unescape($matches[1]).Trim()
      continue
    }

    if ($line -match '^\s*example:\s*"(.*)",\s*$') {
      if ($entryIndex -le 0 -or $entryIndex -gt $TopN) {
        continue
      }

      $indent = ([regex]::Match($line, '^\s*')).Value
      $newExample = New-IdiomaticExample $target.Lang $currentWord $currentDefinition $entryIndex
      $escaped = Escape-Js $newExample
      $lines[$lineIndex] = $indent + 'example: "' + $escaped + '",' 
      $rewritten++
    }
  }

  [System.IO.File]::WriteAllText($path, ([string]::Join($newline, $lines)), $utf8NoBom)

  $summary += [pscustomobject]@{
    File = $target.Path
    TopN = $TopN
    Rewritten = $rewritten
  }
}

Write-Host "=== Idiomatic second-pass rewrite summary (TopN=$TopN) ==="
$summary | Format-Table -AutoSize
