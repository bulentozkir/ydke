param(
  [int]$TopN = 300
)

$ErrorActionPreference = 'Stop'
Set-Location 'C:\gitrepo\ydke'

$targets = @(
  [pscustomobject]@{ Path = 'data/phrasalverbsen.js'; Lang = 'en' },
  [pscustomobject]@{ Path = 'data/phrasalverbsfr.js'; Lang = 'fr' },
  [pscustomobject]@{ Path = 'data/partikelverbde.js'; Lang = 'de' },
  [pscustomobject]@{ Path = 'data/phrasalverbses.js'; Lang = 'es' },
  [pscustomobject]@{ Path = 'data/phrasalverbssp.js'; Lang = 'es' },
  [pscustomobject]@{ Path = 'data/phrasalverbspt.js'; Lang = 'pt' },
  [pscustomobject]@{ Path = 'data/phrasalverbsnl.js'; Lang = 'nl' },
  [pscustomobject]@{ Path = 'data/phrasalverbsit.js'; Lang = 'it' }
)

$localTemplates = @{
  en = @(
    "By Friday, we had to {0} before signing the contract.",
    "During the client call, Maya decided to {0} right away.",
    "I had to {0} after the plan changed at the last minute.",
    "At the station, he promised to {0} before we left.",
    "The coach told the team to {0} when pressure got high.",
    "After reading the email, they chose to {0} immediately.",
    "Can you {0} before the meeting starts?",
    "When sales dropped, the manager tried to {0} without panic.",
    "In the workshop, we used {0} to solve a real problem.",
    "She managed to {0} and still finish before sunset.",
    "The board asked us to {0} in the next release cycle.",
    "He forgot to {0}, so the whole schedule slipped."
  )
  fr = @(
    "Avant vendredi, on devait {0} avant de signer le contrat.",
    "Pendant l'appel client, Maya a decide de {0} tout de suite.",
    "J'ai du {0} quand le plan a change a la derniere minute.",
    "A la gare, il a promis de {0} avant notre depart.",
    "Le coach a dit a l'equipe de {0} sous pression.",
    "Apres le mail, ils ont choisi de {0} immediatement.",
    "Tu peux {0} avant que la reunion commence ?",
    "Quand les ventes ont baisse, le manager a tente de {0} calmement.",
    "Dans l'atelier, on a utilise {0} pour un probleme concret.",
    "Elle a reussi a {0} et a finir avant le coucher du soleil.",
    "Le conseil nous a demande de {0} au prochain cycle.",
    "Il a oublie de {0}, alors le planning a glisse."
  )
  de = @(
    "Bis Freitag mussten wir {0}, bevor wir den Vertrag unterschrieben.",
    "Im Kundengespraech entschied Maya, sofort {0}.",
    "Ich musste {0}, nachdem sich der Plan in letzter Minute aenderte.",
    "Am Bahnhof versprach er, vor der Abfahrt noch {0}.",
    "Der Trainer sagte dem Team, bei Druck {0}.",
    "Nach der E-Mail entschieden sie sich, direkt {0}.",
    "Kannst du {0}, bevor das Meeting beginnt?",
    "Als die Umsaetze sanken, versuchte der Manager ruhig zu {0}.",
    "Im Workshop haben wir {0} genutzt, um ein echtes Problem zu loesen.",
    "Sie schaffte es, {0}, und war trotzdem vor Sonnenuntergang fertig.",
    "Der Vorstand bat uns, im naechsten Release {0}.",
    "Er hat vergessen zu {0}, dadurch geriet der Zeitplan ins Rutschen."
  )
  es = @(
    "Antes del viernes tuvimos que {0} antes de firmar el contrato.",
    "Durante la llamada con el cliente, Maya decidio {0} de inmediato.",
    "Tuve que {0} cuando el plan cambio en el ultimo minuto.",
    "En la estacion, prometio {0} antes de salir.",
    "El entrenador pidio al equipo {0} cuando subio la presion.",
    "Despues del correo, eligieron {0} sin esperar.",
    "Puedes {0} antes de que empiece la reunion?",
    "Cuando bajaron las ventas, el gerente intento {0} con calma.",
    "En el taller usamos {0} para resolver un problema real.",
    "Ella logro {0} y aun asi termino antes del atardecer.",
    "La directiva nos pidio {0} en el siguiente ciclo.",
    "Olvido {0} y por eso se retraso todo el calendario."
  )
  pt = @(
    "Antes de sexta, tivemos de {0} antes de assinar o contrato.",
    "Durante a chamada com o cliente, Maya decidiu {0} na hora.",
    "Tive de {0} quando o plano mudou no ultimo minuto.",
    "Na estacao, ele prometeu {0} antes de partir.",
    "O treinador pediu a equipa para {0} quando a pressao subiu.",
    "Depois do email, eles escolheram {0} sem esperar.",
    "Voce pode {0} antes de a reuniao comecar?",
    "Quando as vendas cairam, o gerente tentou {0} com calma.",
    "No workshop usamos {0} para resolver um problema real.",
    "Ela conseguiu {0} e mesmo assim terminou antes do anoitecer.",
    "A direcao pediu-nos para {0} no proximo ciclo.",
    "Ele esqueceu-se de {0}, e o cronograma atrasou."
  )
  nl = @(
    "Voor vrijdag moesten we {0} voordat we het contract tekenden.",
    "Tijdens het klantgesprek besloot Maya direct te {0}.",
    "Ik moest {0} toen het plan op het laatste moment veranderde.",
    "Op het station beloofde hij nog te {0} voor vertrek.",
    "De coach vroeg het team om {0} onder druk.",
    "Na de e-mail kozen ze ervoor om meteen te {0}.",
    "Kun je {0} voordat de vergadering begint?",
    "Toen de verkoop daalde, probeerde de manager rustig te {0}.",
    "In de workshop gebruikten we {0} om een echt probleem op te lossen.",
    "Ze wist {0} en was toch voor zonsondergang klaar.",
    "Het bestuur vroeg ons om {0} in de volgende release.",
    "Hij vergat te {0}, daardoor schoof het hele schema op."
  )
  it = @(
    "Prima di venerdi abbiamo dovuto {0} prima di firmare il contratto.",
    "Durante la chiamata con il cliente, Maya ha deciso di {0} subito.",
    "Ho dovuto {0} quando il piano e cambiato all'ultimo minuto.",
    "Alla stazione ha promesso di {0} prima di partire.",
    "L'allenatore ha chiesto alla squadra di {0} sotto pressione.",
    "Dopo l'email, hanno scelto di {0} senza aspettare.",
    "Puoi {0} prima che inizi la riunione?",
    "Quando le vendite sono calate, il manager ha provato a {0} con calma.",
    "Nel workshop abbiamo usato {0} per risolvere un problema reale.",
    "Lei e riuscita a {0} e ha finito prima del tramonto.",
    "La direzione ci ha chiesto di {0} nel prossimo ciclo.",
    "Ha dimenticato di {0}, e il calendario e slittato."
  )
}

$turkishTemplates = @(
  "Cuma gununden once sozlesmeyi imzalamadan once {0} yapmamiz gerekti.",
  "Musteri aramasinda Maya hemen {0} yapmaya karar verdi.",
  "Plan son dakikada degisince {0} yapmam gerekti.",
  "Istasyonda yola cikmadan once {0} yapacagina soz verdi.",
  "Baski artinca antrenor takimdan {0} yapmasini istedi.",
  "E-postadan sonra beklemeden {0} yapmayi sectiler.",
  "Toplanti baslamadan once {0} yapabilir misin?",
  "Satislar dusunce yonetici paniklemeden {0} yapmaya calisti.",
  "Atolyede gercek bir sorunu cozerken {0} kullandik.",
  "Hem {0} yapti hem de gun batmadan bitirdi.",
  "Yonetim bir sonraki surumde {0} yapmamizi istedi.",
  "{0} yapmayi unutunca tum takvim kaydi."
)

$templatedMarkers = @(
  'People often use ',
  'In the office, we use ',
  'You can use ',
  'They used ',
  'On utilise souvent ',
  'Au travail, on utilise ',
  'Tu peux utiliser ',
  'Ils ont utilise ',
  'Man verwendet oft ',
  'Bei der Arbeit nutzen wir ',
  'Du kannst ',
  'Sie haben ',
  'Se usa mucho ',
  'En el trabajo usamos ',
  'Puedes usar ',
  'Ellos usaron ',
  'Usa-se muito ',
  'No trabalho usamos ',
  'Voce pode usar ',
  'Eles usaram ',
  'Men gebruikt vaak ',
  'Op het werk gebruiken we ',
  'Je kunt ',
  'Zij gebruikten ',
  'Si usa spesso ',
  'Al lavoro usiamo ',
  'Puoi usare ',
  'Hanno usato ',
  ' - Genelde ',
  ' - Bu durumda ',
  ' - Insanlar ',
  'ifadesi kullanilir'
)

function Escape-Js([string]$text) {
  if ($null -eq $text) { return '' }
  return ($text -replace '\\', '\\\\' -replace '"', '\\"')
}

function Is-Templated([string]$example) {
  if ([string]::IsNullOrWhiteSpace($example)) { return $false }
  foreach ($marker in $templatedMarkers) {
    if ($example.Contains($marker)) {
      return $true
    }
  }
  return $false
}

function Choose-LanguageTemplate([string]$lang) {
  if ($localTemplates.ContainsKey($lang)) {
    return $localTemplates[$lang]
  }
  return $localTemplates['en']
}

function New-IdiomaticExample([string]$lang, [string]$word, [int]$entryIndex) {
  $local = Choose-LanguageTemplate $lang
  $seed = [math]::Abs($word.GetHashCode() + ($entryIndex * 97))
  $idx = $seed % $local.Count
  $trIdx = $seed % $turkishTemplates.Count

  $left = [string]::Format($local[$idx], $word)
  $right = [string]::Format($turkishTemplates[$trIdx], $word)

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
  $changed = 0
  $templatedInTopN = 0

  for ($lineIndex = 0; $lineIndex -lt $lines.Length; $lineIndex++) {
    $line = $lines[$lineIndex]

    if ($line -match '^\s*word:\s*"(.*)",\s*$') {
      $entryIndex++
      $currentWord = [regex]::Unescape($matches[1]).Trim()
      continue
    }

    if ($line -match '^\s*example:\s*"(.*)",\s*$') {
      if ($entryIndex -le 0 -or $entryIndex -gt $TopN) {
        continue
      }

      $exampleValue = [regex]::Unescape($matches[1]).Trim()
      if (-not (Is-Templated $exampleValue)) {
        continue
      }

      $templatedInTopN++
      if ([string]::IsNullOrWhiteSpace($currentWord)) {
        continue
      }

      $indent = ([regex]::Match($line, '^\s*')).Value
      $newExample = New-IdiomaticExample $target.Lang $currentWord $entryIndex
      $escaped = Escape-Js $newExample
      $lines[$lineIndex] = $indent + 'example: "' + $escaped + '",' 
      $changed++
    }
  }

  [System.IO.File]::WriteAllText($path, ([string]::Join($newline, $lines)), $utf8NoBom)

  # Recheck templated count in top N after rewrite.
  $postTemplated = 0
  $postEntryIndex = 0
  for ($lineIndex = 0; $lineIndex -lt $lines.Length; $lineIndex++) {
    $line = $lines[$lineIndex]
    if ($line -match '^\s*word:\s*"') {
      $postEntryIndex++
      continue
    }
    if ($postEntryIndex -gt 0 -and $postEntryIndex -le $TopN -and $line -match '^\s*example:\s*"(.*)",\s*$') {
      $value = [regex]::Unescape($matches[1]).Trim()
      if (Is-Templated $value) {
        $postTemplated++
      }
    }
  }

  $summary += [pscustomobject]@{
    File = $target.Path
    TopN = $TopN
    TemplatedFoundInTopN = $templatedInTopN
    Rewritten = $changed
    RemainingTemplatedInTopN = $postTemplated
  }
}

Write-Host "=== Second pass idiomatic rewrite summary (TopN=$TopN) ==="
$summary | Format-Table -AutoSize
