# -*- coding: utf-8 -*-
"""Backfill data/wordsa1.js with genuine CEFR-J A1 vocabulary.

Every word below is an A1 headword in the CEFR-J vocabulary profile
(https://github.com/openlanguageprofiles/olp-en-cefrj) that our A1 list did
not yet contain. Definitions and examples are hand-written to the same
standard as the original hand-authored part of the file:

  * definition - a full English sentence (capital, closing period), then
    " - ", then a Turkish gloss with common synonyms after semicolons
  * example    - a natural A1 sentence that actually uses the headword,
    then " - ", then its Turkish translation

Pure grammar words ('m, 're, is, was, an, to, those ...) from the CEFR-J list
are intentionally NOT added: they do not work as flashcards.

Run: python scripts/backfill_en_a1.py --apply
"""
import io
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PATH = os.path.join(ROOT, "data", "wordsa1.js")

# (word, pos, definition_en, definition_tr, example_en, example_tr)
WORDS = [
    ("agree", "verb", "To have the same opinion as someone.", "Aynı fikirde olmak; katılmak.", "I agree with you.", "Sana katılıyorum."),
    ("airplane", "noun", "A vehicle with wings that flies.", "Uçak.", "The airplane lands at six.", "Uçak saat altıda iniyor."),
    ("almost", "adverb", "Very nearly but not completely.", "Neredeyse; hemen hemen.", "Dinner is almost ready.", "Yemek neredeyse hazır."),
    ("alone", "adjective", "Without any other people.", "Yalnız; tek başına.", "She lives alone.", "O yalnız yaşıyor."),
    ("along", "preposition", "Moving from one end of something to the other.", "Boyunca.", "We walked along the river.", "Nehir boyunca yürüdük."),
    ("already", "adverb", "Before now or before a certain time.", "Zaten; çoktan.", "I have already eaten.", "Ben çoktan yedim."),
    ("anybody", "pronoun", "Any person at all.", "Herhangi biri; kimse.", "Is anybody at home?", "Evde kimse var mı?"),
    ("anyone", "pronoun", "Any person at all.", "Herhangi biri; kimse.", "Anyone can join the club.", "Kulübe herkes katılabilir."),
    ("apron", "noun", "A cloth you wear to keep your clothes clean while cooking.", "Önlük; mutfak önlüğü.", "My father wears an apron when he cooks.", "Babam yemek yaparken önlük giyer."),
    ("baseball", "noun", "A game played with a bat and a small ball.", "Beyzbol.", "They play baseball after school.", "Okuldan sonra beyzbol oynuyorlar."),
    ("basketball", "noun", "A game where players throw a ball into a high net.", "Basketbol.", "My brother is good at basketball.", "Ağabeyim basketbolda iyidir."),
    ("bean", "noun", "A seed that is eaten as a vegetable.", "Fasulye.", "I eat beans and rice.", "Fasulye ve pilav yerim."),
    ("beef", "noun", "Meat from a cow.", "Sığır eti; dana eti.", "We had beef for dinner.", "Akşam yemeğinde sığır eti yedik."),
    ("birth", "noun", "The time when a baby comes into the world.", "Doğum.", "What is your date of birth?", "Doğum tarihiniz nedir?"),
    ("biscuit", "noun", "A small thin sweet cake that is hard.", "Bisküvi.", "Would you like a biscuit?", "Bisküvi ister misin?"),
    ("blank", "adjective", "With nothing written or printed on it.", "Boş; yazısız.", "Write your name on the blank line.", "Adını boş satıra yaz."),
    ("blow", "verb", "To send air out of your mouth.", "Üflemek; esmek.", "Blow out the candles!", "Mumları üfle!"),
    ("bone", "noun", "One of the hard parts inside the body.", "Kemik.", "The dog is eating a bone.", "Köpek bir kemik yiyor."),
    ("bookstore", "noun", "A shop that sells books.", "Kitapçı; kitabevi.", "I bought this map at the bookstore.", "Bu haritayı kitapçıdan aldım."),
    ("borrow", "verb", "To take something for a short time and give it back.", "Ödünç almak.", "Can I borrow your pen?", "Kalemini ödünç alabilir miyim?"),
    ("bowl", "noun", "A deep round dish for food.", "Kase; kâse.", "There is soup in the bowl.", "Kasede çorba var."),
    ("brain", "noun", "The part inside your head that lets you think.", "Beyin.", "The brain needs rest too.", "Beynin de dinlenmeye ihtiyacı var."),
    ("bridge", "noun", "A structure built over a river or road.", "Köprü.", "We crossed the old bridge.", "Eski köprüden geçtik."),
    ("bright", "adjective", "Giving out a lot of light.", "Parlak; aydınlık.", "The sun is very bright today.", "Bugün güneş çok parlak."),
    ("brush", "noun", "A tool with short hairs used for cleaning or painting.", "Fırça.", "Where is my hair brush?", "Saç fırçam nerede?"),
    ("bucket", "noun", "A round open container with a handle.", "Kova.", "Fill the bucket with water.", "Kovayı suyla doldur."),
    ("butterfly", "noun", "An insect with large colourful wings.", "Kelebek.", "A butterfly landed on the flower.", "Bir kelebek çiçeğe kondu."),
    ("button", "noun", "A small round object that closes clothes.", "Düğme.", "A button fell off my shirt.", "Gömleğimden bir düğme düştü."),
    ("camp", "noun", "A place where people stay in tents.", "Kamp.", "We stayed at a summer camp.", "Yaz kampında kaldık."),
    ("care", "verb", "To feel that something is important to you.", "Önemsemek; umursamak.", "I care about my friends.", "Arkadaşlarımı önemserim."),
    ("careful", "adjective", "Giving attention so you do not have an accident.", "Dikkatli.", "Be careful on the stairs.", "Merdivenlerde dikkatli ol."),
    ("carefully", "adverb", "In a way that gives attention to what you do.", "Dikkatlice; dikkatli bir şekilde.", "She drove carefully.", "Dikkatlice araba kullandı."),
    ("cartoon", "noun", "A film made with drawings.", "Çizgi film; karikatür.", "Children love this cartoon.", "Çocuklar bu çizgi filmi çok sever."),
    ("catch", "verb", "To take hold of something moving through the air.", "Yakalamak; tutmak.", "Catch the ball!", "Topu yakala!"),
    ("celebrate", "verb", "To do something special because of a happy event.", "Kutlamak.", "We celebrate my birthday in May.", "Doğum günümü mayısta kutlarız."),
    ("character", "noun", "A person in a book, film, or play.", "Karakter; kişilik.", "My favourite character is the doctor.", "En sevdiğim karakter doktor."),
    ("church", "noun", "A building where Christians pray.", "Kilise.", "The church is next to the park.", "Kilise parkın yanında."),
    ("circle", "noun", "A perfectly round shape.", "Daire; çember.", "Draw a circle on the paper.", "Kâğıda bir daire çiz."),
    ("classmate", "noun", "Someone who is in the same class as you.", "Sınıf arkadaşı.", "Ali is my classmate.", "Ali benim sınıf arkadaşım."),
    ("clever", "adjective", "Able to learn and understand things quickly.", "Akıllı; zeki.", "She is a clever student.", "O akıllı bir öğrenci."),
    ("cloud", "noun", "A white or grey mass of water in the sky.", "Bulut.", "There is one cloud in the sky.", "Gökyüzünde bir bulut var."),
    ("cloudy", "adjective", "With many clouds in the sky.", "Bulutlu.", "It is cloudy this morning.", "Bu sabah hava bulutlu."),
    ("code", "noun", "A set of numbers or letters used to identify something.", "Kod; şifre.", "What is the code for this door?", "Bu kapının şifresi nedir?"),
    ("collect", "verb", "To bring things together as a hobby.", "Toplamak; biriktirmek.", "I collect old coins.", "Eski madenî paralar biriktiririm."),
    ("conversation", "noun", "A talk between two or more people.", "Konuşma; sohbet.", "We had a long conversation.", "Uzun bir sohbet ettik."),
    ("copy", "verb", "To make something that is the same as another thing.", "Kopyalamak; çoğaltmak.", "Please copy this page.", "Lütfen bu sayfayı kopyala."),
    ("corn", "noun", "A tall plant with yellow seeds that people eat.", "Mısır.", "We ate corn at the picnic.", "Piknikte mısır yedik."),
    ("corner", "noun", "The place where two lines or streets meet.", "Köşe.", "The shop is on the corner.", "Dükkân köşede."),
    ("cry", "verb", "To have tears coming from your eyes.", "Ağlamak.", "The baby began to cry.", "Bebek ağlamaya başladı."),
    ("cute", "adjective", "Pretty and pleasant to look at.", "Sevimli; şirin.", "What a cute puppy!", "Ne sevimli bir köpek yavrusu!"),
    ("describe", "verb", "To say what someone or something is like.", "Tanımlamak; tarif etmek.", "Can you describe your house?", "Evini tarif edebilir misin?"),
    ("design", "noun", "The way something is planned and made.", "Tasarım.", "I like the design of this chair.", "Bu sandalyenin tasarımını beğendim."),
    ("difference", "noun", "The way in which two things are not the same.", "Fark; ayrım.", "What is the difference between them?", "Aralarındaki fark nedir?"),
    ("doll", "noun", "A toy that looks like a small person.", "Oyuncak bebek.", "The girl carries her doll everywhere.", "Kız oyuncak bebeğini her yere götürür."),
    ("drama", "noun", "A play for the theatre, television, or radio.", "Drama; tiyatro oyunu.", "We watched a drama last night.", "Dün gece bir drama izledik."),
    ("dream", "noun", "The pictures you see in your mind while you sleep.", "Rüya; hayal.", "I had a strange dream.", "Garip bir rüya gördüm."),
    ("drop", "verb", "To let something fall.", "Düşürmek; damla.", "Do not drop the glass.", "Bardağı düşürme."),
    ("drum", "noun", "A musical instrument you hit with your hands or sticks.", "Davul; trampet.", "He plays the drum in a band.", "Bir grupta davul çalıyor."),
    ("engineer", "noun", "A person who designs machines, roads, or buildings.", "Mühendis.", "My sister is an engineer.", "Ablam mühendis."),
    ("everyday", "adjective", "Happening or used every day; ordinary.", "Her günkü; günlük; sıradan.", "These are my everyday shoes.", "Bunlar günlük ayakkabılarım."),
    ("everywhere", "adverb", "In or to all places.", "Her yerde; her yere.", "I looked everywhere for my keys.", "Anahtarlarımı her yerde aradım."),
    ("excellent", "adjective", "Extremely good.", "Mükemmel; harika.", "Your English is excellent.", "İngilizcen mükemmel."),
    ("factory", "noun", "A building where things are made by machines.", "Fabrika.", "He works in a car factory.", "Bir otomobil fabrikasında çalışıyor."),
    ("fair", "adjective", "Treating everyone in the same right way.", "Adil; dürüst.", "That is not fair!", "Bu adil değil!"),
    ("fan", "noun", "A person who likes a sport, singer, or team very much.", "Hayran; taraftar.", "I am a fan of this team.", "Bu takımın taraftarıyım."),
    ("feeling", "noun", "Something you feel inside, such as happiness or fear.", "Duygu; his.", "I had a strange feeling.", "Garip bir his duydum."),
    ("fever", "noun", "A body temperature that is higher than normal.", "Ateş; yüksek ateş.", "The child has a fever.", "Çocuğun ateşi var."),
    ("field", "noun", "An open area of land used for grass or crops.", "Tarla; alan; saha.", "Cows are in the field.", "İnekler tarlada."),
    ("fight", "verb", "To use force against someone.", "Kavga etmek; dövüşmek.", "Do not fight with your brother.", "Kardeşinle kavga etme."),
    ("fishing", "noun", "The activity of catching fish.", "Balık tutma; balıkçılık.", "We go fishing on Sundays.", "Pazar günleri balık tutmaya gideriz."),
    ("flag", "noun", "A piece of cloth with the colours of a country.", "Bayrak.", "The flag is red and white.", "Bayrak kırmızı ve beyaz."),
    ("focus", "verb", "To give all your attention to one thing.", "Odaklanmak; yoğunlaşmak.", "Please focus on your work.", "Lütfen işine odaklan."),
    ("foggy", "adjective", "With thick cloud close to the ground.", "Sisli.", "The road was foggy this morning.", "Bu sabah yol sisliydi."),
    ("foreign", "adjective", "Belonging to a country that is not your own.", "Yabancı.", "She speaks two foreign languages.", "İki yabancı dil konuşuyor."),
    ("frog", "noun", "A small green animal that jumps and lives near water.", "Kurbağa.", "A frog jumped into the pond.", "Bir kurbağa gölete atladı."),
    ("gift", "noun", "Something you give to someone.", "Hediye; armağan.", "This gift is for you.", "Bu hediye senin için."),
    ("goal", "noun", "Something you want to do in the future; a point in football.", "Hedef; amaç; gol.", "My goal is to learn English.", "Hedefim İngilizce öğrenmek."),
    ("gold", "noun", "A valuable yellow metal.", "Altın.", "The ring is made of gold.", "Yüzük altından yapılmış."),
    ("grammar", "noun", "The rules for making sentences in a language.", "Dilbilgisi; gramer.", "English grammar is not easy.", "İngilizce dilbilgisi kolay değil."),
    ("grape", "noun", "A small round green or purple fruit.", "Üzüm.", "These grapes are very sweet.", "Bu üzümler çok tatlı."),
    ("grass", "noun", "The green plant that covers gardens and fields.", "Çim; çimen; ot.", "The children sat on the grass.", "Çocuklar çimenlere oturdu."),
    ("gray", "adjective", "Having the colour between black and white.", "Gri.", "He wore a gray coat.", "Gri bir palto giymişti."),
    ("greet", "verb", "To say hello to someone.", "Selamlamak; karşılamak.", "She greeted us at the door.", "Bizi kapıda karşıladı."),
    ("ground", "noun", "The surface of the earth.", "Yer; zemin; toprak.", "The ball fell on the ground.", "Top yere düştü."),
    ("guest", "noun", "A person you invite to your home or a hotel visitor.", "Misafir; konuk.", "We have guests tonight.", "Bu gece misafirlerimiz var."),
    ("guy", "noun", "A man.", "Adam; herif.", "That guy is my neighbour.", "O adam komşum."),
    ("habit", "noun", "Something you do often, almost without thinking.", "Alışkanlık.", "Reading is a good habit.", "Okumak iyi bir alışkanlıktır."),
    ("hall", "noun", "The space just inside the front door of a building.", "Hol; koridor; salon.", "Leave your boots in the hall.", "Botlarını holde bırak."),
    ("hamburger", "noun", "A round piece of meat eaten in a bread roll.", "Hamburger.", "I ordered a hamburger.", "Bir hamburger sipariş ettim."),
    ("handsome", "adjective", "Good-looking, usually said about a man.", "Yakışıklı.", "Her brother is very handsome.", "Erkek kardeşi çok yakışıklı."),
    ("headache", "noun", "A pain in your head.", "Baş ağrısı.", "I have a bad headache.", "Şiddetli bir baş ağrım var."),
    ("heart", "noun", "The organ that pumps blood around your body.", "Kalp; yürek.", "My heart beats fast.", "Kalbim hızlı atıyor."),
    ("heavy", "adjective", "Weighing a lot; difficult to lift.", "Ağır.", "This box is too heavy.", "Bu kutu çok ağır."),
    ("hide", "verb", "To put something where nobody can see it.", "Saklamak; gizlenmek.", "Hide the presents!", "Hediyeleri sakla!"),
    ("hill", "noun", "A high area of land, smaller than a mountain.", "Tepe.", "Their house is on a hill.", "Evleri bir tepenin üzerinde."),
    ("hold", "verb", "To have something in your hands.", "Tutmak.", "Hold my bag, please.", "Lütfen çantamı tut."),
    ("hole", "noun", "An empty space in something solid.", "Delik; çukur.", "There is a hole in my sock.", "Çorabımda bir delik var."),
    ("hometown", "noun", "The town where you were born or grew up.", "Memleket; doğduğu şehir.", "Izmir is my hometown.", "İzmir benim memleketim."),
    ("hurry", "verb", "To do something or go somewhere quickly.", "Acele etmek.", "Hurry, the bus is coming!", "Acele et, otobüs geliyor!"),
    ("hurt", "verb", "To cause pain to someone or yourself.", "Acıtmak; incitmek; canı yanmak.", "My leg hurts.", "Bacağım acıyor."),
    ("imagine", "verb", "To make a picture of something in your mind.", "Hayal etmek; düşünmek.", "Imagine a big blue house.", "Büyük mavi bir ev hayal et."),
    ("inside", "preposition", "In the space within something.", "İçinde; içeri.", "The cat is inside the box.", "Kedi kutunun içinde."),
    ("item", "noun", "One single thing in a list or group.", "Madde; öğe; parça.", "The first item is milk.", "İlk madde süt."),
    ("jump", "verb", "To push yourself into the air with your legs.", "Zıplamak; atlamak.", "The cat jumped onto the table.", "Kedi masaya atladı."),
    ("kick", "verb", "To hit something with your foot.", "Tekmelemek; vurmak.", "He kicked the ball hard.", "Topa sert vurdu."),
    ("kid", "noun", "A child.", "Çocuk.", "The kids are playing outside.", "Çocuklar dışarıda oynuyor."),
    ("king", "noun", "The male ruler of a country.", "Kral.", "The king lived in a castle.", "Kral bir şatoda yaşadı."),
    ("kiss", "verb", "To touch someone with your lips to show love.", "Öpmek.", "She kissed her son goodnight.", "Oğlunu iyi geceler diye öptü."),
    ("kite", "noun", "A toy that flies in the wind on a string.", "Uçurtma.", "We flew a kite at the beach.", "Sahilde uçurtma uçurduk."),
    ("knee", "noun", "The joint in the middle of your leg.", "Diz.", "I hurt my knee playing football.", "Futbol oynarken dizimi incittim."),
    ("knife", "noun", "A tool with a sharp blade used for cutting.", "Bıçak.", "Cut the bread with a knife.", "Ekmeği bıçakla kes."),
    ("lady", "noun", "A polite word for a woman.", "Hanım; bayan; hanımefendi.", "A lady asked for directions.", "Bir hanım yol sordu."),
    ("lazy", "adjective", "Not wanting to work or be active.", "Tembel.", "Do not be lazy!", "Tembellik etme!"),
    ("leader", "noun", "The person who is in charge of a group.", "Lider; önder.", "She is the leader of our team.", "O bizim takımımızın lideri."),
    ("lovely", "adjective", "Very nice or beautiful.", "Hoş; güzel; sevimli.", "What a lovely garden!", "Ne güzel bir bahçe!"),
    ("luck", "noun", "Good or bad things that happen by chance.", "Şans; talih.", "Good luck on your exam!", "Sınavında bol şans!"),
    ("lucky", "adjective", "Having good things happen by chance.", "Şanslı.", "You are very lucky.", "Çok şanslısın."),
    ("mail", "noun", "Letters and packages sent by post.", "Posta; mektup.", "The mail arrives at nine.", "Posta dokuzda geliyor."),
    ("medicine", "noun", "Something you take to make you well.", "İlaç; tıp.", "Take this medicine twice a day.", "Bu ilacı günde iki kez al."),
    ("memory", "noun", "The ability to remember things.", "Hafıza; bellek; anı.", "She has a very good memory.", "Çok iyi bir hafızası var."),
    ("mind", "noun", "The part of you that thinks and remembers.", "Akıl; zihin.", "Keep an open mind.", "Zihnini açık tut."),
    ("monkey", "noun", "An animal with a long tail that climbs trees.", "Maymun.", "The monkey ate a banana.", "Maymun bir muz yedi."),
    ("moon", "noun", "The bright object that we see in the sky at night.", "Ay.", "The moon is very bright tonight.", "Bu gece ay çok parlak."),
    ("musician", "noun", "A person who plays music.", "Müzisyen.", "My uncle is a musician.", "Amcam müzisyen."),
    ("nationality", "noun", "The country that you belong to.", "Uyruk; milliyet.", "What is your nationality?", "Uyruğunuz nedir?"),
    ("neck", "noun", "The part of the body between the head and shoulders.", "Boyun.", "My neck hurts.", "Boynum ağrıyor."),
    ("noise", "noun", "A sound, especially a loud or unpleasant one.", "Gürültü; ses.", "Do not make so much noise.", "Bu kadar gürültü yapma."),
    ("officer", "noun", "A person with a position of authority, as in the police.", "Memur; subay; polis memuru.", "The officer asked for my ticket.", "Memur biletimi istedi."),
    ("owner", "noun", "The person that something belongs to.", "Sahip; mal sahibi.", "Who is the owner of this car?", "Bu arabanın sahibi kim?"),
    ("palace", "noun", "A very large house where a king or queen lives.", "Saray.", "We visited an old palace.", "Eski bir sarayı ziyaret ettik."),
    ("pants", "noun", "A piece of clothing worn on the legs.", "Pantolon.", "These pants are too long.", "Bu pantolon çok uzun."),
    ("peace", "noun", "A time without war or fighting.", "Barış; huzur.", "Everyone wants peace.", "Herkes barış istiyor."),
    ("pet", "noun", "An animal that you keep at home.", "Evcil hayvan.", "Do you have a pet?", "Evcil hayvanın var mı?"),
    ("pick", "verb", "To choose something or take it up.", "Seçmek; toplamak.", "Pick a card.", "Bir kart seç."),
    ("picnic", "noun", "A meal eaten outdoors.", "Piknik.", "We had a picnic in the park.", "Parkta piknik yaptık."),
    ("pocket", "noun", "A small bag sewn into your clothes.", "Cep.", "My phone is in my pocket.", "Telefonum cebimde."),
    ("pollution", "noun", "Dirty air, water, or land.", "Kirlilik; çevre kirliliği.", "Air pollution is a big problem.", "Hava kirliliği büyük bir sorun."),
    ("poster", "noun", "A large printed picture put on a wall.", "Poster; afiş.", "There is a poster on my wall.", "Duvarımda bir poster var."),
    ("push", "verb", "To move something away from you with your hands.", "İtmek.", "Push the door to open it.", "Açmak için kapıyı it."),
    ("rainy", "adjective", "With a lot of rain.", "Yağmurlu.", "It was a rainy day.", "Yağmurlu bir gündü."),
    ("rat", "noun", "An animal like a large mouse.", "Sıçan; fare.", "There is a rat in the garden.", "Bahçede bir sıçan var."),
    ("reporter", "noun", "A person who writes news for a paper or television.", "Muhabir; gazeteci.", "The reporter asked many questions.", "Muhabir çok soru sordu."),
    ("review", "noun", "A report that says what is good or bad about something.", "İnceleme; eleştiri; gözden geçirme.", "I read a review of the film.", "Filmin bir eleştirisini okudum."),
    ("ribbon", "noun", "A long thin piece of cloth used for tying.", "Kurdele; şerit.", "She tied her hair with a ribbon.", "Saçını kurdeleyle bağladı."),
    ("ring", "noun", "A round piece of jewellery worn on a finger.", "Yüzük; halka.", "She wears a gold ring.", "Altın bir yüzük takıyor."),
    ("role", "noun", "The part an actor plays, or a person's job in a group.", "Rol; görev.", "He plays the role of the father.", "Baba rolünü oynuyor."),
    ("rude", "adjective", "Not polite.", "Kaba; nezaketsiz.", "It is rude to interrupt.", "Sözünü kesmek kabalıktır."),
    ("sale", "noun", "The act of selling, or a time of lower prices.", "Satış; indirim.", "The shoes are on sale.", "Ayakkabılar indirimde."),
    ("save", "verb", "To keep money, or to help someone out of danger.", "Biriktirmek; kurtarmak; kaydetmek.", "I save money every month.", "Her ay para biriktiririm."),
    ("seat", "noun", "A place where you can sit.", "Koltuk; oturak; yer.", "Please take a seat.", "Lütfen oturun."),
    ("sell", "verb", "To give something to someone for money.", "Satmak.", "They sell fresh bread here.", "Burada taze ekmek satıyorlar."),
    ("shake", "verb", "To move something quickly up and down.", "Sallamak; titremek.", "Shake the bottle before you drink.", "İçmeden önce şişeyi salla."),
    ("share", "verb", "To give part of something to another person.", "Paylaşmak.", "Please share your books.", "Lütfen kitaplarınızı paylaşın."),
    ("ship", "noun", "A large boat that travels on the sea.", "Gemi.", "The ship left the port.", "Gemi limandan ayrıldı."),
    ("shoulder", "noun", "The part of the body between the neck and the arm.", "Omuz.", "He put a bag on his shoulder.", "Omzuna bir çanta astı."),
    ("side", "noun", "One of the flat outer parts of something.", "Yan; taraf; kenar.", "Write on both sides of the paper.", "Kâğıdın iki tarafına da yaz."),
    ("sign", "noun", "A board with words or pictures that gives information.", "Tabela; işaret; levha.", "The sign says 'Stop'.", "Tabelada 'Dur' yazıyor."),
    ("sir", "noun", "A polite way to speak to a man.", "Efendim; beyefendi.", "Good morning, sir.", "Günaydın efendim."),
    ("size", "noun", "How big or small something is.", "Boyut; beden; ölçü.", "What size are your shoes?", "Ayakkabı numaran kaç?"),
    ("sky", "noun", "The space above the earth where clouds are.", "Gökyüzü; gök.", "The sky is blue today.", "Bugün gökyüzü mavi."),
    ("smell", "verb", "To notice something with your nose.", "Koklamak; kokmak.", "The flowers smell wonderful.", "Çiçekler harika kokuyor."),
    ("smile", "verb", "To make a happy expression with your mouth.", "Gülümsemek.", "She smiled at me.", "Bana gülümsedi."),
    ("smoke", "noun", "The grey gas that comes from a fire.", "Duman.", "Smoke came from the kitchen.", "Mutfaktan duman geldi."),
    ("soccer", "noun", "A game played by two teams with a round ball.", "Futbol.", "We play soccer every Friday.", "Her cuma futbol oynarız."),
    ("speech", "noun", "A formal talk given to a group of people.", "Konuşma; söylev.", "The teacher gave a short speech.", "Öğretmen kısa bir konuşma yaptı."),
    ("stage", "noun", "The raised floor where actors perform.", "Sahne; aşama.", "The singer walked onto the stage.", "Şarkıcı sahneye çıktı."),
    ("star", "noun", "A small bright light in the night sky.", "Yıldız.", "I can see many stars tonight.", "Bu gece birçok yıldız görebiliyorum."),
    ("stay", "verb", "To remain in one place.", "Kalmak.", "We stayed at a small hotel.", "Küçük bir otelde kaldık."),
    ("step", "noun", "One movement of your foot when walking.", "Adım; basamak.", "Take one step forward.", "Bir adım öne gel."),
    ("stone", "noun", "A small piece of rock.", "Taş.", "He threw a stone into the lake.", "Göle bir taş attı."),
    ("straight", "adjective", "Going in one direction without a bend.", "Düz; doğru.", "Draw a straight line.", "Düz bir çizgi çiz."),
    ("strange", "adjective", "Unusual or surprising.", "Garip; tuhaf; yabancı.", "I heard a strange noise.", "Garip bir ses duydum."),
    ("subway", "noun", "A train that runs under a city.", "Metro.", "I take the subway to work.", "İşe metroyla giderim."),
    ("successful", "adjective", "Having achieved what you wanted.", "Başarılı.", "She is a successful doctor.", "O başarılı bir doktor."),
    ("surprise", "noun", "Something you did not expect.", "Sürpriz; şaşkınlık.", "The party was a surprise.", "Parti bir sürprizdi."),
    ("sweet", "adjective", "Tasting like sugar.", "Tatlı.", "This tea is too sweet.", "Bu çay çok tatlı."),
    ("technology", "noun", "Machines and methods based on modern science.", "Teknoloji.", "Technology changes very fast.", "Teknoloji çok hızlı değişiyor."),
    ("thick", "adjective", "Wide between one side and the other.", "Kalın; yoğun.", "It is a thick book.", "Kalın bir kitap."),
    ("thin", "adjective", "Not thick, or not fat.", "İnce; zayıf.", "Cut the bread into thin slices.", "Ekmeği ince dilimler hâlinde kes."),
    ("throw", "verb", "To send something through the air with your hand.", "Atmak; fırlatmak.", "Throw the ball to me.", "Topu bana at."),
    ("tiger", "noun", "A large wild cat with orange fur and black stripes.", "Kaplan.", "The tiger is a strong animal.", "Kaplan güçlü bir hayvandır."),
    ("tool", "noun", "An object you use with your hands to do a job.", "Alet; araç.", "A hammer is a useful tool.", "Çekiç faydalı bir alettir."),
    ("touch", "verb", "To put your hand on something.", "Dokunmak.", "Do not touch the wet paint.", "Islak boyaya dokunma."),
    ("towel", "noun", "A cloth used for drying yourself.", "Havlu.", "Take a towel to the beach.", "Sahile bir havlu götür."),
    ("tower", "noun", "A tall narrow building.", "Kule.", "We climbed the old tower.", "Eski kuleye tırmandık."),
    ("toy", "noun", "An object that children play with.", "Oyuncak.", "The baby dropped her toy.", "Bebek oyuncağını düşürdü."),
    ("truck", "noun", "A large vehicle for carrying goods.", "Kamyon.", "The truck is full of boxes.", "Kamyon kutularla dolu."),
    ("vase", "noun", "A container used for holding flowers.", "Vazo.", "Put the roses in the vase.", "Gülleri vazoya koy."),
    ("volleyball", "noun", "A game where two teams hit a ball over a net.", "Voleybol.", "She plays volleyball at school.", "Okulda voleybol oynuyor."),
    ("waitress", "noun", "A woman who serves food in a restaurant.", "Garson; kadın garson.", "The waitress brought our menu.", "Garson menümüzü getirdi."),
    ("war", "noun", "A time of fighting between countries.", "Savaş.", "The war lasted four years.", "Savaş dört yıl sürdü."),
    ("wheel", "noun", "A round object that turns and moves a vehicle.", "Tekerlek.", "The car has four wheels.", "Arabanın dört tekerleği var."),
    ("wind", "noun", "Air that moves quickly outside.", "Rüzgâr.", "The wind is strong today.", "Bugün rüzgâr kuvvetli."),
    ("wish", "verb", "To want something to happen.", "Dilemek; istemek.", "I wish you good luck.", "Sana iyi şanslar dilerim."),
    ("worry", "verb", "To think that something bad may happen.", "Endişelenmek; merak etmek.", "Do not worry about the exam.", "Sınav için endişelenme."),
    ("yogurt", "noun", "A thick food made from milk.", "Yoğurt.", "I eat yogurt every morning.", "Her sabah yoğurt yerim."),
    ("zoo", "noun", "A place where wild animals are kept for people to see.", "Hayvanat bahçesi.", "We saw lions at the zoo.", "Hayvanat bahçesinde aslan gördük."),
]

TEMPLATE = (
    "  {\n"
    '    word: "%s",\n'
    '    pos: "%s",\n'
    '    level: "A1",\n'
    '    category: "General",\n'
    '    definition: "%s - %s",\n'
    '    example: "%s - %s",\n'
    "  },\n"
)


def main():
    dry = "--apply" not in sys.argv
    text = io.open(PATH, encoding="utf-8").read()
    existing = {w.lower() for w in re.findall(r'word:\s*"((?:\\.|[^"\\])*)"', text)}

    new, skipped = [], []
    for word, pos, den, dtr, exen, extr in WORDS:
        if word.lower() in existing:
            skipped.append(word)
            continue
        for field in (den, dtr, exen, extr):
            assert '"' not in field, (word, field)
        new.append(TEMPLATE % (word, pos, den, dtr, exen, extr))
        existing.add(word.lower())

    print("authored %d, already present %d, adding %d"
          % (len(WORDS), len(skipped), len(new)))
    if skipped:
        print("  skipped: " + ", ".join(skipped))
    if dry:
        print("DRY RUN - pass --apply to write")
        return

    marker = text.rindex("];")
    out = text[:marker] + "".join(new) + text[marker:]
    if out.count("{") != out.count("}"):
        print("ABORT: brace imbalance")
        return 1
    io.open(PATH, "w", encoding="utf-8", newline="\n").write(out)
    print("written -> %d entries" % len(re.findall(r'word:\s*"', out)))


if __name__ == "__main__":
    sys.exit(main() or 0)
