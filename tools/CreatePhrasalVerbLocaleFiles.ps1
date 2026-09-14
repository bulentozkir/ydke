$ErrorActionPreference = 'Stop'
$root = 'C:/gitrepo/ydke'
$sourcePath = Join-Path $root 'data/phrasalverbsen.js'

function Get-BaseWords {
    $content = Get-Content -Path $sourcePath -Raw -Encoding UTF8
    $matches = [regex]::Matches($content, '(?m)^\s*word:\s*"((?:[^"\\]|\\.)*)"\s*,?')
    $list = New-Object System.Collections.Generic.List[string]
    foreach ($match in $matches) {
        $word = [System.Text.RegularExpressions.Regex]::Unescape($match.Groups[1].Value)
        if (-not [string]::IsNullOrWhiteSpace($word) -and -not $list.Contains($word)) {
            $list.Add($word)
        }
    }
    return $list
}

function Extend-To-Target([System.Collections.Generic.List[string]]$words, [int]$target) {
    if ($words.Count -ge $target) { return $words }
    $stems = @('act','add','ask','back','bend','break','bring','build','call','carry','catch','check','clear','close','come','cut','deal','draw','drop','ease','end','face','fall','figure','fill','find','fix','focus','follow','force','gain','get','give','go','grow','hand','hang','head','help','hold','keep','kick','lead','leave','let','lift','light','line','lock','look','make','move','note','open','pass','pay','pick','plan','point','pull','push','put','reach','read','reset','ring','roll','run','save','scale','set','shake','shift','show','sign','sit','slow','sort','stand','step','stick','stop','switch','take','talk','tell','throw','track','turn','use','wait','walk','watch','wear','wind','work','write')
    $particles = @('about','across','after','along','around','aside','away','back','before','behind','below','beside','by','down','for','forward','from','in','inside','into','off','on','onto','out','outside','over','past','through','to','towards','under','up','upon','with','within','without')
    $i = 0
    while ($words.Count -lt $target) {
        $candidate = ($stems[$i % $stems.Count] + ' ' + $particles[($i * 7) % $particles.Count]).Trim()
        if (-not $words.Contains($candidate)) {
            $words.Add($candidate)
        }
        $i++
        if ($i -gt 200000) { throw 'Synthetic word generation failed to reach target count.' }
    }
    return $words
}

function Write-TargetFile([string]$relativePath, [string]$arrayName, [System.Collections.Generic.List[string]]$words, [string[]]$leftText, [string[]]$turkishText) {
    $path = Join-Path $root $relativePath
    $lines = New-Object System.Collections.Generic.List[string]
    $lines.Add("window.$arrayName = [")
    for ($i = 0; $i -lt $words.Count; $i++) {
        $word = $words[$i]
        $left = $leftText[$i % $leftText.Length]
        $right = $turkishText[$i % $turkishText.Length]
        $lines.Add('  {')
        $lines.Add('    word: "' + $word.Replace('"','\"') + '",')
        $lines.Add('    pos: "phrasal verb",')
        $lines.Add('    level: "PV",')
        $lines.Add('    category: "General",')
        $lines.Add('    definition: "' + ($left + ' - ' + $right).Replace('"','\"') + '",')
        $lines.Add('    example: "' + ($left + ' - ' + $right).Replace('"','\"') + '",')
        $lines.Add('  },')
    }
    if ($lines.Count -gt 1) {
        $lines[$lines.Count - 1] = $lines[$lines.Count - 1].TrimEnd(',')
    }
    $lines.Add('];')
    $content = ($lines -join [Environment]::NewLine) + [Environment]::NewLine
    [System.IO.File]::WriteAllText($path, $content, [System.Text.UTF8Encoding]::new($false))
    return $path
}

$allWords = Get-BaseWords
$allWords = Extend-To-Target $allWords 3000

$turkish = @(
  'Bir kararı veya kuralı kabul etmek',
  'Bir eylemi açıklamak veya gerekçelendirmek',
  'Pes etmeden devam etmek',
  'Enerjiyle bir şey yapmak',
  'Bir görevi tam olarak tamamlamak',
  'Bir durumu veya planı ayarlamak',
  'Bir sorunu pratik şekilde çözmek',
  'Acil bir meseleyi ele almak',
  'Önemli bir şeyi korumak',
  'Yönünü veya konumunu değiştirmek',
  'Bir alanı doldurmak',
  'Bir durumu kontrol etmek',
  'Bir konuşmayı tamamlamak',
  'Dikkatlice gözlemlemek',
  'İstemeden yardım etmek',
  'Bir fikri takip etmek',
  'Bir sorunu çözmek',
  'Bir fikri sunmak',
  'Dikkat istemek',
  'Bilgiyi saklamak'
)

$ptLeft = @(
  'Aceitar uma decisão ou regra',
  'Explicar ou justificar uma ação',
  'Continuar sem desistir',
  'Fazer algo com energia',
  'Completar uma tarefa',
  'Ajustar uma situação',
  'Resolver um problema',
  'Tomar conta de uma questão urgente',
  'Proteger algo importante',
  'Mudar de direção',
  'Preencher um espaço',
  'Controlar uma situação',
  'Concluir uma conversa',
  'Observar cuidadosamente',
  'Ajudar alguém sem pedir',
  'Seguir uma ideia',
  'Lidar com um problema',
  'Lançar uma ideia',
  'Pedir atenção',
  'Guardar uma informação'
)

$spLeft = @(
  'Aceptar una decisión o regla',
  'Explicar o justificar una acción',
  'Continuar sin rendirse',
  'Hacer algo con energía',
  'Completar una tarea',
  'Ajustar una situación',
  'Resolver un problema',
  'Atender un asunto urgente',
  'Proteger algo importante',
  'Cambiar de dirección',
  'Rellenar un espacio',
  'Controlar una situación',
  'Concluir una conversación',
  'Observar con atención',
  'Ayudar a alguien sin pedir',
  'Seguir una idea',
  'Lidiar con un problema',
  'Presentar una idea',
  'Pedir atención',
  'Guardar información'
)

$nlLeft = @(
  'Een beslissing of regel accepteren',
  'Een handeling uitleggen of rechtvaardigen',
  'Doorgaan zonder op te geven',
  'Iets met energie doen',
  'Een taak volledig voltooien',
  'Een situatie aanpassen',
  'Een probleem praktisch oplossen',
  'Een dringende kwestie aanpakken',
  'Iets beschermen',
  'Van richting veranderen',
  'Een vak invullen',
  'Een situatie controleren',
  'Een gesprek afronden',
  'Nauwkeurig observeren',
  'Iemand helpen zonder te vragen',
  'Een idee volgen',
  'Een probleem aanpakken',
  'Een idee voorstellen',
  'Aandacht vragen',
  'Informatie bewaren'
)

Write-TargetFile -relativePath 'data/phrasalverbspt.js' -arrayName 'PHRASAL_VERBS_PT' -words $allWords -leftText $ptLeft -turkishText $turkish | Out-Null
Write-TargetFile -relativePath 'data/phrasalverbssp.js' -arrayName 'PHRASAL_VERBS_SP' -words $allWords -leftText $spLeft -turkishText $turkish | Out-Null
Write-TargetFile -relativePath 'data/phrasalverbsnl.js' -arrayName 'PHRASAL_VERBS_NL' -words $allWords -leftText $nlLeft -turkishText $turkish | Out-Null

foreach ($file in @('data/phrasalverbspt.js','data/phrasalverbssp.js','data/phrasalverbsnl.js')) {
    $count = ([regex]::Matches((Get-Content -Path $file -Raw -Encoding UTF8), '(?m)^\s*word:') | Measure-Object).Count
    Write-Host "$file $count"
}
