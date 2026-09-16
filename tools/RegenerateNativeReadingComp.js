const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..', 'data');
const locales = {
  it: {
    global: 'READING_PASSAGES_IT',
    rows: [
      ['A1', 'La mia mattina', 'Ogni mattina Luca si alza alle sette. Fa colazione con pane e marmellata, poi va a scuola in autobus. Nel pomeriggio gioca a calcio con gli amici.', ['Luca si alza alle sette', 'pane e marmellata', 'in autobus', 'gioca a calcio']],
      ['A1', 'Il gatto di Marta', 'Marta ha un gatto bianco che si chiama Neve. Neve dorme sul divano durante il giorno e corre in giardino la sera. Marta gli dà il cibo due volte al giorno.', ['bianco', 'Neve', 'sul divano', 'due volte al giorno']],
      ['A1', 'Al mercato', 'Sabato Elena va al mercato con suo padre. Comprano mele rosse, pomodori e un pezzo di formaggio. Poi tornano a casa a piedi perché il mercato è vicino.', ['sabato', 'con suo padre', 'mele rosse', 'a piedi']],
      ['A2', 'Una giornata piovosa', 'Quando piove, Paolo porta un ombrello blu. Legge un libro vicino alla finestra e beve una cioccolata calda. La sera ascolta la musica in salotto.', ['blu', 'vicino alla finestra', 'cioccolata calda', 'in salotto']],
      ['A2', 'La bicicletta nuova', 'Giulia riceve una bicicletta nuova per il compleanno. La bicicletta è verde e ha un cestino davanti. La domenica Giulia va al parco con sua sorella.', ['per il compleanno', 'verde', 'un cestino', 'al parco']],
      ['A2', 'Il viaggio in treno', 'Marco prende il treno per visitare la nonna a Firenze. Il viaggio dura due ore. Durante il tragitto guarda il paesaggio e legge una rivista.', ['la nonna', 'Firenze', 'due ore', 'una rivista']],
      ['B1', 'Un progetto scolastico', 'La classe di Sara prepara un piccolo orto nel cortile della scuola. Gli studenti piantano pomodori, basilico e lattuga. Ogni gruppo annaffia le piante in un giorno diverso.', ['un orto', 'nel cortile', 'basilico', 'un giorno diverso']],
      ['B1', 'La biblioteca di quartiere', 'La biblioteca organizza un incontro per i lettori giovani. Un’autrice parla dei suoi libri e risponde alle domande. Alla fine ogni partecipante riceve un segnalibro.', ['un incontro', 'un’autrice', 'alle domande', 'un segnalibro']],
    ],
  },
  pt: {
    global: 'READING_PASSAGES_PT',
    rows: [
      ['A1', 'A manhã de Ana', 'Todas as manhãs, Ana acorda às sete. Ela toma café com pão e fruta e vai para a escola de autocarro. À tarde, brinca com a sua amiga Sofia.', ['às sete', 'pão e fruta', 'de autocarro', 'Sofia']],
      ['A1', 'O cão do Miguel', 'Miguel tem um cão castanho chamado Tico. Tico dorme numa cama perto da cozinha e gosta de correr no jardim. Miguel dá-lhe água depois do passeio.', ['castanho', 'Tico', 'perto da cozinha', 'água']],
      ['A1', 'No mercado', 'No sábado, Beatriz vai ao mercado com a mãe. Elas compram laranjas, cenouras e queijo. Depois voltam para casa a pé porque o mercado fica perto.', ['no sábado', 'com a mãe', 'laranjas', 'a pé']],
      ['A2', 'Uma tarde de chuva', 'Quando chove, Rui leva um guarda-chuva amarelo. Ele lê junto à janela e bebe chá quente. À noite, joga um jogo de tabuleiro com a família.', ['amarelo', 'junto à janela', 'chá quente', 'um jogo de tabuleiro']],
      ['A2', 'A bicicleta azul', 'Joana recebe uma bicicleta azul no seu aniversário. A bicicleta tem uma cesta pequena. Aos domingos, Joana pedala até ao parque com o irmão.', ['no aniversário', 'azul', 'uma cesta pequena', 'ao parque']],
      ['A2', 'Uma viagem de comboio', 'Pedro viaja de comboio para visitar a avó no Porto. A viagem dura três horas. Durante o caminho, ele observa a paisagem e escreve no seu caderno.', ['a avó', 'no Porto', 'três horas', 'no caderno']],
      ['B1', 'A horta da escola', 'A turma de Carolina cria uma horta no pátio da escola. Os alunos plantam tomates, ervas aromáticas e alfaces. Cada grupo rega as plantas num dia diferente.', ['uma horta', 'no pátio', 'ervas aromáticas', 'num dia diferente']],
      ['B1', 'A biblioteca do bairro', 'A biblioteca prepara uma tarde para os leitores jovens. Um escritor apresenta o seu novo livro e responde às perguntas. No fim, todos recebem um marcador de livros.', ['uma tarde', 'um escritor', 'às perguntas', 'um marcador de livros']],
    ],
  },
  es: {
    global: 'READING_PASSAGES_ES',
    rows: [
      ['A1', 'La mañana de Ana', 'Cada mañana, Ana se levanta a las siete. Desayuna pan con fruta y va al colegio en autobús. Por la tarde juega con su amiga Sofía.', ['a las siete', 'pan con fruta', 'en autobús', 'Sofía']],
      ['A1', 'El perro de Miguel', 'Miguel tiene un perro marrón que se llama Tico. Tico duerme en una cama cerca de la cocina y corre en el jardín. Miguel le da agua después del paseo.', ['marrón', 'Tico', 'cerca de la cocina', 'agua']],
      ['A1', 'En el mercado', 'El sábado, Beatriz va al mercado con su madre. Compran naranjas, zanahorias y queso. Después vuelven a casa a pie porque el mercado está cerca.', ['el sábado', 'con su madre', 'naranjas', 'a pie']],
      ['A2', 'Una tarde de lluvia', 'Cuando llueve, Rui lleva un paraguas amarillo. Lee junto a la ventana y bebe té caliente. Por la noche juega a un juego de mesa con su familia.', ['amarillo', 'junto a la ventana', 'té caliente', 'un juego de mesa']],
      ['A2', 'La bicicleta azul', 'Joana recibe una bicicleta azul por su cumpleaños. La bicicleta tiene una cesta pequeña. Los domingos, Joana va en bicicleta al parque con su hermano.', ['por su cumpleaños', 'azul', 'una cesta pequeña', 'al parque']],
      ['A2', 'Un viaje en tren', 'Pedro viaja en tren para visitar a su abuela en Valencia. El viaje dura tres horas. Durante el trayecto observa el paisaje y escribe en su cuaderno.', ['a su abuela', 'Valencia', 'tres horas', 'en su cuaderno']],
      ['B1', 'El huerto de la escuela', 'La clase de Carolina prepara un huerto en el patio de la escuela. Los alumnos plantan tomates, hierbas y lechugas. Cada grupo riega las plantas en un día diferente.', ['un huerto', 'en el patio', 'lechugas', 'un día diferente']],
      ['B1', 'La biblioteca del barrio', 'La biblioteca organiza una tarde para jóvenes lectores. Una escritora presenta su nuevo libro y responde a las preguntas. Al final, todos reciben un marcapáginas.', ['una tarde', 'una escritora', 'a las preguntas', 'un marcapáginas']],
    ],
  },
  nl: {
    global: 'READING_PASSAGES_NL',
    rows: [
      ['A1', 'De ochtend van Noor', 'Elke ochtend staat Noor om zeven uur op. Ze eet brood met fruit en gaat met de bus naar school. In de middag speelt ze met haar vriendin Emma.', ['om zeven uur', 'brood met fruit', 'met de bus', 'Emma']],
      ['A1', 'De hond van Milan', 'Milan heeft een bruine hond die Tico heet. Tico slaapt in een mand bij de keuken en rent graag in de tuin. Na de wandeling geeft Milan hem water.', ['bruin', 'Tico', 'bij de keuken', 'water']],
      ['A1', 'Op de markt', 'Op zaterdag gaat Beatriz met haar moeder naar de markt. Ze kopen sinaasappels, wortels en kaas. Daarna lopen ze naar huis, want de markt is dichtbij.', ['op zaterdag', 'met haar moeder', 'sinaasappels', 'naar huis']],
      ['A2', 'Een regenachtige middag', 'Als het regent, neemt Rui een gele paraplu mee. Hij leest bij het raam en drinkt warme thee. ’s Avonds speelt hij een bordspel met zijn familie.', ['geel', 'bij het raam', 'warme thee', 'een bordspel']],
      ['A2', 'De blauwe fiets', 'Joana krijgt voor haar verjaardag een blauwe fiets. De fiets heeft een klein mandje. Op zondag fietst Joana met haar broer naar het park.', ['voor haar verjaardag', 'blauw', 'een klein mandje', 'naar het park']],
      ['A2', 'Een treinreis', 'Pedro reist met de trein naar zijn oma in Utrecht. De reis duurt drie uur. Onderweg kijkt hij naar het landschap en schrijft hij in zijn schrift.', ['zijn oma', 'Utrecht', 'drie uur', 'in zijn schrift']],
      ['B1', 'De schooltuin', 'De klas van Carolina maakt een tuin op de speelplaats van de school. De leerlingen planten tomaten, kruiden en sla. Elke groep geeft op een andere dag water.', ['een tuin', 'op de speelplaats', 'sla', 'op een andere dag']],
      ['B1', 'De buurtbibliotheek', 'De bibliotheek organiseert een middag voor jonge lezers. Een schrijfster vertelt over haar nieuwe boek en beantwoordt vragen. Aan het einde krijgt iedereen een boekenlegger.', ['een middag', 'een schrijfster', 'vragen', 'een boekenlegger']],
    ],
  },
};

const questionText = {
  it: ['Che cosa dice il testo sul dettaglio {0}?', 'Leggi di nuovo il brano e cerca questo dettaglio.', 'Il testo dice: "{1}".'],
  pt: ['O que diz o texto sobre o detalhe {0}?', 'Lê novamente o texto e procura este detalhe.', 'O texto diz: "{1}".'],
  es: ['¿Qué dice el texto sobre el detalle {0}?', 'Lee de nuevo el texto y busca este detalle.', 'El texto dice: "{1}".'],
  nl: ['Wat staat er in de tekst over detail {0}?', 'Lees de tekst opnieuw en zoek dit detail.', 'De tekst zegt: "{1}".'],
};

const distractors = {
  it: ['un altro luogo', 'qualcosa di diverso', 'non è scritto nel testo'],
  pt: ['outro lugar', 'algo diferente', 'não está no texto'],
  es: ['otro lugar', 'algo diferente', 'no aparece en el texto'],
  nl: ['een andere plaats', 'iets anders', 'dat staat niet in de tekst'],
};

const turkishAnswers = {
  it: {
    'Luca si alza alle sette': 'Luca saat yedide kalkar', 'pane e marmellata': 'ekmek ve reçel', 'in autobus': 'otobüsle', 'gioca a calcio': 'futbol oynar',
    bianco: 'beyaz', Neve: 'Neve', 'sul divano': 'kanepede', 'due volte al giorno': 'günde iki kez', sabato: 'cumartesi', 'con suo padre': 'babasıyla', 'mele rosse': 'kırmızı elmalar', 'a piedi': 'yürüyerek', blu: 'mavi', 'vicino alla finestra': 'pencerenin yanında', 'cioccolata calda': 'sıcak çikolata', 'in salotto': 'oturma odasında', 'per il compleanno': 'doğum günü için', verde: 'yeşil', 'un cestino': 'bir sepet', 'al parco': 'parka', 'la nonna': 'büyükanne', Firenze: 'Floransa', 'due ore': 'iki saat', 'una rivista': 'bir dergi', 'un orto': 'bir sebze bahçesi', 'nel cortile': 'avluda', basilico: 'fesleğen', 'un giorno diverso': 'farklı bir gün', 'un incontro': 'bir buluşma', 'un’autrice': 'bir kadın yazar', 'alle domande': 'sorulara', 'un segnalibro': 'bir kitap ayracı'
  },
  pt: {
    'às sete': 'saat yedide', 'pão e fruta': 'ekmek ve meyve', 'de autocarro': 'otobüsle', Sofia: 'Sofia', castanho: 'kahverengi', Tico: 'Tico', 'perto da cozinha': 'mutfağın yanında', água: 'su', 'no sábado': 'cumartesi günü', 'com a mãe': 'annesiyle', laranjas: 'portakallar', 'a pé': 'yürüyerek', amarelo: 'sarı', 'junto à janela': 'pencerenin yanında', 'chá quente': 'sıcak çay', 'um jogo de tabuleiro': 'bir masa oyunu', 'no aniversário': 'doğum gününde', azul: 'mavi', 'uma cesta pequena': 'küçük bir sepet', 'ao parque': 'parka', 'a avó': 'büyükanne', 'no Porto': 'Porto’da', 'três horas': 'üç saat', 'no caderno': 'defterde', 'uma horta': 'bir sebze bahçesi', 'no pátio': 'avluda', 'ervas aromáticas': 'aromatik otlar', 'num dia diferente': 'farklı bir günde', 'uma tarde': 'bir öğleden sonra', 'um escritor': 'bir erkek yazar', 'às perguntas': 'sorulara', 'um marcador de livros': 'bir kitap ayracı'
  },
  es: {
    'a las siete': 'saat yedide', 'pan con fruta': 'meyveli ekmek', 'en autobús': 'otobüsle', Sofía: 'Sofía', marrón: 'kahverengi', Tico: 'Tico', 'cerca de la cocina': 'mutfağın yanında', agua: 'su', 'el sábado': 'cumartesi günü', 'con su madre': 'annesiyle', naranjas: 'portakallar', 'a pie': 'yürüyerek', amarillo: 'sarı', 'junto a la ventana': 'pencerenin yanında', 'té caliente': 'sıcak çay', 'un juego de mesa': 'bir masa oyunu', 'por su cumpleaños': 'doğum günü için', azul: 'mavi', 'una cesta pequeña': 'küçük bir sepet', 'al parque': 'parka', 'a su abuela': 'büyükannesini', Valencia: 'Valensiya', 'tres horas': 'üç saat', 'en su cuaderno': 'defterine', 'un huerto': 'bir sebze bahçesi', 'en el patio': 'avluda', lechugas: 'marullar', 'un día diferente': 'farklı bir gün', 'una tarde': 'bir öğleden sonra', 'una escritora': 'bir kadın yazar', 'a las preguntas': 'sorulara', 'un marcapáginas': 'bir kitap ayracı'
  },
  nl: {
    'om zeven uur': 'saat yedide', 'brood met fruit': 'meyveli ekmek', 'met de bus': 'otobüsle', Emma: 'Emma', bruin: 'kahverengi', Tico: 'Tico', 'bij de keuken': 'mutfağın yanında', water: 'su', 'op zaterdag': 'cumartesi günü', 'met haar moeder': 'annesiyle', sinaasappels: 'portakallar', 'naar huis': 'eve', geel: 'sarı', 'bij het raam': 'pencerenin yanında', 'warme thee': 'sıcak çay', 'een bordspel': 'bir masa oyunu', 'voor haar verjaardag': 'doğum günü için', blauw: 'mavi', 'een klein mandje': 'küçük bir sepet', 'naar het park': 'parka', 'zijn oma': 'büyükannesi', Utrecht: 'Utrecht', 'drie uur': 'üç saat', 'in zijn schrift': 'defterine', 'een tuin': 'bir bahçe', 'op de speelplaats': 'oyun alanında', sla: 'marul', 'op een andere dag': 'başka bir günde', 'een middag': 'bir öğleden sonra', 'een schrijfster': 'bir kadın yazar', vragen: 'sorular', 'een boekenlegger': 'bir kitap ayracı'
  },
};

function makeQuestions(row, language) {
  const answers = row[3];
  const text = questionText[language];
  return answers.map((answer, index) => {
    const options = [answer, ...distractors[language]];
    return {
      q: text[0].replace('{0}', index + 1),
      options,
      correct: 0,
      hint: text[1],
      explain: `${text[2].replace('{1}', answer)} - ${turkishAnswers[language][answer]}`,
    };
  });
}

for (const [language, config] of Object.entries(locales)) {
  const passages = config.rows.map((row) => ({
    level: row[0],
    title: row[1],
    text: row[2],
    questions: makeQuestions(row, language),
  }));
  const output = `window.${config.global} = ${JSON.stringify(passages, null, 2)};\n`;
  fs.writeFileSync(path.join(root, `readingcomp${language}.js`), output, 'utf8');
  console.log(`${language}: passages=${passages.length} questions=${passages.reduce((n, p) => n + p.questions.length, 0)}`);
}
