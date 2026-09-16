$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dataRoot = Join-Path $root 'data'
$translationPath = Join-Path $PSScriptRoot 'nativePhrasalTurkish.json'
$translations = Get-Content -Path $translationPath -Raw -Encoding UTF8 | ConvertFrom-Json

$sets = [ordered]@{
    es = @(
        'darse cuenta de|comprender o advertir algo|Me di cuenta del error demasiado tarde.|I realized the mistake too late.'
        'echar de menos|sentir la ausencia de alguien|Echo de menos a mi familia.|I miss my family.'
        'llevarse bien con|tener una buena relación|Ana se lleva bien con sus vecinos.|Ana gets along well with her neighbors.'
        'ponerse de acuerdo|llegar a un acuerdo|Nos pusimos de acuerdo sobre el plan.|We agreed on the plan.'
        'contar con|poder confiar en algo o alguien|Puedes contar con mi ayuda.|You can count on my help.'
        'depender de|estar condicionado por algo|Todo depende de la decisión final.|Everything depends on the final decision.'
        'tratar de|intentar hacer algo|Trato de llegar temprano.|I try to arrive early.'
        'acabar de|haber hecho algo hace poco|Acabo de terminar el informe.|I have just finished the report.'
        'dejar de|interrumpir una acción|Dejó de fumar el año pasado.|He stopped smoking last year.'
        'volver a|hacer algo otra vez|Volveremos a hablar mañana.|We will talk again tomorrow.'
        'empezar a|comenzar una acción|Empezó a estudiar italiano.|She started studying Italian.'
        'soñar con|desear intensamente algo|Sueña con viajar por el mundo.|She dreams of traveling the world.'
        'pensar en|tener algo en mente|Pienso en mis hijos cada día.|I think about my children every day.'
        'confiar en|tener confianza en alguien|Confío en tu experiencia.|I trust your experience.'
        'insistir en|mantener una petición o idea|Insistió en pagar la cuenta.|He insisted on paying the bill.'
        'fijarse en|prestar atención a algo|Fíjate en los detalles.|Pay attention to the details.'
        'enamorarse de|empezar a amar a alguien|Se enamoró de su compañero de clase.|She fell in love with her classmate.'
        'preocuparse por|sentir inquietud por algo|No te preocupes por el resultado.|Do not worry about the result.'
        'acostumbrarse a|adaptarse a una situación|Me estoy acostumbrando al clima.|I am getting used to the weather.'
        'enterarse de|llegar a saber algo|Me enteré de la noticia ayer.|I found out about the news yesterday.'
    )
    fr = @(
        'compter sur|se fier à une personne ou quelque chose|Tu peux compter sur moi.|You can count on me.'
        "s'occuper de|prendre en charge|Elle s'occupe des enfants.|She takes care of the children."
        "tenir à|accorder de l'importance à|Je tiens beaucoup à cette amitié.|I care a lot about this friendship."
        'se souvenir de|garder un souvenir en mémoire|Il se souvient de son enfance.|He remembers his childhood.'
        'avoir besoin de|nécessiter quelque chose|Nous avons besoin de plus de temps.|We need more time.'
        "avoir envie de|désirer quelque chose|J'ai envie de voyager.|I feel like traveling."
        'dépendre de|être conditionné par|Tout dépend de la météo.|Everything depends on the weather.'
        "essayer de|faire un effort pour|J'essaie de comprendre.|I am trying to understand."
        'arrêter de|cesser une action|Il a arrêté de fumer.|He stopped smoking.'
        'venir de|avoir fait quelque chose récemment|Je viens de rentrer.|I have just come back.'
        'commencer à|débuter une action|Elle commence à travailler tôt.|She starts working early.'
        'continuer à|poursuivre une action|Nous continuons à apprendre.|We continue learning.'
        "penser à|avoir quelqu'un ou quelque chose à l'esprit|Je pense souvent à toi.|I often think about you."
        'croire en|avoir confiance en|Je crois en tes capacités.|I believe in your abilities.'
        'faire attention à|être vigilant|Fais attention à la marche.|Watch the step.'
        "s'intéresser à|éprouver de l'intérêt|Il s'intéresse à la musique.|He is interested in music."
        'se méfier de|ne pas faire confiance|Méfie-toi de cette promesse.|Beware of that promise.'
        'parler de|aborder un sujet|Nous parlons de nos projets.|We are talking about our plans.'
        'rêver de|désirer vivement|Elle rêve de devenir médecin.|She dreams of becoming a doctor.'
        'renoncer à|abandonner une idée ou un projet|Il a renoncé à son voyage.|He gave up his trip.'
    )
    it = @(
        'avere bisogno di|necessitare di qualcosa|Ho bisogno di più tempo.|I need more time.'
        'avere voglia di|desiderare qualcosa|Ho voglia di un caffè.|I feel like having a coffee.'
        "andare d'accordo con|avere un buon rapporto|Vado d'accordo con i colleghi.|I get along with my colleagues."
        'contare su|fare affidamento su|Puoi contare su di me.|You can count on me.'
        'dipendere da|essere condizionato da|Tutto dipende dal tempo.|Everything depends on the weather.'
        'pensare a|avere in mente|Penso spesso a te.|I often think about you.'
        'credere in|avere fiducia in|Credo nelle tue capacità.|I believe in your abilities.'
        'fidarsi di|avere fiducia in qualcuno|Mi fido di lei.|I trust her.'
        'occuparsi di|prendersi cura o gestire|Si occupa dei bambini.|She takes care of the children.'
        'prendersi cura di|badare a qualcuno o qualcosa|Ci prendiamo cura del giardino.|We take care of the garden.'
        "smettere di|cessare un'azione|Ha smesso di fumare.|He stopped smoking."
        'cercare di|tentare di fare|Cerco di capire.|I try to understand.'
        'riuscire a|essere capace di|È riuscita a finire il lavoro.|She managed to finish the work.'
        'imparare a|acquisire la capacità di|Sto imparando a cucinare.|I am learning to cook.'
        'cominciare a|iniziare a fare|Cominciamo a lavorare alle otto.|We start working at eight.'
        "finire di|terminare un'azione|Ho finito di leggere il libro.|I finished reading the book."
        'partecipare a|prendere parte a|Partecipo alla riunione.|I am attending the meeting.'
        'rinunciare a|abbandonare qualcosa|Non voglio rinunciare al progetto.|I do not want to give up the project.'
        'preoccuparsi di|avere preoccupazione per|Non preoccuparti del risultato.|Do not worry about the result.'
        'innamorarsi di|cominciare ad amare|Si è innamorata di lui.|She fell in love with him.'
    )
    nl = @(
        'opstaan|uit bed komen|Ik sta om zeven uur op.|I get up at seven.'
        'aankomen|een bestemming bereiken|De trein komt om acht uur aan.|The train arrives at eight.'
        'uitgaan|naar buiten gaan voor ontspanning|We gaan vanavond uit.|We are going out tonight.'
        'meegaan|samen ergens naartoe gaan|Ga je met ons mee?|Are you coming with us?'
        'terugkomen|opnieuw terugkeren|Ik kom morgen terug.|I will come back tomorrow.'
        'doorgaan|verdergaan|De les gaat door.|The lesson continues.'
        'afspreken|een afspraak maken|We spreken morgen af.|We are meeting tomorrow.'
        'uitproberen|iets testen|Ik wil deze app uitproberen.|I want to try this app.'
        'opzoeken|informatie of iemand zoeken|Ik zoek het woord op.|I look up the word.'
        'meenemen|iets met je dragen|Neem je een jas mee?|Are you taking a coat?'
        'terugbrengen|iets terug naar een plaats brengen|Breng het boek morgen terug.|Bring the book back tomorrow.'
        'aandoen|kleding aantrekken of licht inschakelen|Doe je jas aan.|Put on your coat.'
        'uitdoen|kleding verwijderen of licht uitschakelen|Doe het licht uit.|Turn off the light.'
        'aanzetten|een apparaat inschakelen|Zet de computer aan.|Turn on the computer.'
        'uitzetten|een apparaat uitschakelen|Zet de televisie uit.|Turn off the television.'
        'opbellen|telefoneren|Ik bel je vanavond op.|I will call you tonight.'
        'afwassen|vaat schoonmaken|Na het eten was ik af.|I wash the dishes after dinner.'
        'beginnen met|starten met iets|We beginnen met de oefening.|We start with the exercise.'
        'stoppen met|ophouden met iets|Hij stopt met roken.|He stops smoking.'
        'denken aan|iets in gedachten hebben|Ik denk aan mijn familie.|I think about my family.'
        'wachten op|blijven tot iemand of iets komt|We wachten op de bus.|We are waiting for the bus.'
        'houden van|veel waarderen of liefhebben|Ik hou van muziek.|I love music.'
        'luisteren naar|aandachtig horen|Luister naar de leraar.|Listen to the teacher.'
        'praten over|een onderwerp bespreken|We praten over het plan.|We are talking about the plan.'
        'zorgen voor|verantwoordelijkheid nemen voor|Zij zorgt voor haar broer.|She takes care of her brother.'
        'geloven in|vertrouwen hebben in|Ik geloof in jou.|I believe in you.'
        'rekenen op|vertrouwen op|Je kunt op mij rekenen.|You can count on me.'
        'afhangen van|afhankelijk zijn van|Het hangt van het weer af.|It depends on the weather.'
        'zich voorbereiden op|klaarmaken voor|We bereiden ons op het examen voor.|We prepare for the exam.'
    )
    pt = @(
        'dar-se conta de|perceber ou compreender algo|Ela se deu conta do erro.|She realized the mistake.'
        'levar em conta|considerar algo|Devemos levar em conta os custos.|We should take the costs into account.'
        'gostar de|ter apreço por algo|Gosto de música brasileira.|I like Brazilian music.'
        'precisar de|necessitar de algo|Preciso de mais tempo.|I need more time.'
        'depender de|estar condicionado a algo|Tudo depende do resultado.|Everything depends on the result.'
        'lembrar-se de|ter algo na memória|Lembro-me da reunião.|I remember the meeting.'
        'esquecer-se de|não se lembrar de algo|Esqueci-me do compromisso.|I forgot the appointment.'
        'cuidar de|tomar conta de alguém ou algo|Ela cuida dos filhos.|She takes care of the children.'
        'tratar de|ocupar-se de algo|Vou tratar do assunto amanhã.|I will deal with the matter tomorrow.'
        'pensar em|ter algo em mente|Penso em você todos os dias.|I think about you every day.'
        'acreditar em|ter fé ou confiança|Acredito nas suas capacidades.|I believe in your abilities.'
        'confiar em|ter confiança em alguém|Confio em você.|I trust you.'
        'insistir em|manter uma exigência|Ele insistiu em pagar.|He insisted on paying.'
        'sonhar com|desejar intensamente|Ela sonha com uma casa grande.|She dreams of a big house.'
        'concordar com|ter a mesma opinião|Concordo com você.|I agree with you.'
        'contar com|poder confiar em alguém|Pode contar comigo.|You can count on me.'
        'lidar com|tratar de uma situação|Precisamos lidar com o problema.|We need to deal with the problem.'
        'acabar de|ter feito algo recentemente|Acabei de chegar.|I have just arrived.'
        'começar a|iniciar uma ação|Comecei a estudar cedo.|I started studying early.'
        'deixar de|parar de fazer algo|Ele deixou de fumar.|He stopped smoking.'
        'voltar a|fazer novamente|Vou voltar a tentar.|I will try again.'
        'aprender a|adquirir uma habilidade|Estou aprendendo a cozinhar.|I am learning to cook.'
        'conseguir|ser capaz de|Consegui terminar o trabalho.|I managed to finish the work.'
        'participar de|tomar parte em algo|Participei da reunião.|I took part in the meeting.'
        'desistir de|abandonar uma tentativa|Não vou desistir do projeto.|I will not give up the project.'
        'preocupar-se com|sentir inquietação por algo|Não se preocupe com isso.|Do not worry about that.'
        'apaixonar-se por|começar a amar alguém|Ela se apaixonou por ele.|She fell in love with him.'
        'optar por|escolher algo|Optamos por uma solução simples.|We chose a simple solution.'
        'recorrer a|pedir ajuda ou usar algo|Recorremos ao especialista.|We turned to the specialist.'
    )
}

$globals = @{ es = 'PHRASAL_VERBS_ES'; fr = 'PHRASAL_VERBS_FR'; it = 'PHRASAL_VERBS_IT'; nl = 'PHRASAL_VERBS_NL'; pt = 'PHRASAL_VERBS_PT' }
foreach ($language in $sets.Keys) {
    $records = foreach ($row in $sets[$language]) {
        $parts = $row -split '\|', 4
        $translationKey = "$language|$($parts[2])"
        $turkishExample = $translations.PSObject.Properties[$translationKey].Value
        if ([string]::IsNullOrWhiteSpace($turkishExample)) {
            throw "Missing Turkish translation for $translationKey."
        }
        [ordered]@{
            word = $parts[0]
            pos = 'multi-word verb'
            level = 'PV'
            category = 'General'
            definition = "$($parts[1]) - $($parts[3])"
            example = "$($parts[2]) - $turkishExample"
        }
    }
    $json = $records | ConvertTo-Json -Depth 4
    $content = "window.$($globals[$language]) = $json;`r`n"
    [IO.File]::WriteAllText((Join-Path $dataRoot "phrasalverbs$language.js"), $content, [Text.UTF8Encoding]::new($false))
    Write-Host "$language records=$($records.Count)"
}
