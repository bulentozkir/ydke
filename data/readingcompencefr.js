// Reading Comprehension passages -- English CEFR-level track (A1-C2).
// All passages and questions below are ORIGINAL content written for this
// app -- they are NOT copied or adapted from any third-party site.
// (test-english.com/reading/ was consulted only for structural inspiration
// -- their Terms of Use explicitly prohibit copying/republishing: "You may
// not copy, download, print, frame, or republish any material from this
// website in any form or on any other website... All materials, including
// test questions..., are protected by international copyright law." --
// so their actual texts/questions were intentionally NOT used as a
// source here.)
// Schema: { level, title, text, questions: [ { q, options: [4], correct,
// hint, explain } ] }. `correct` is the 0-based index into `options` of
// the right answer. This is a SEPARATE, parallel English track alongside
// the original grade-based one (data/readingcomp.js / window.
// READING_PASSAGES, grades 1-12) -- both stay available; the in-page
// "By Grade" / "By CEFR Level" toggle (English only) picks which one is
// active. Used ONLY by the Reading Comprehension game
// (readingcomprehension.js), selected when currentLang === "en" and the
// CEFR mode is active.

window.READING_PASSAGES_EN_CEFR = [
  // ===================== A1 =====================
  {
    level: "A1",
    title: "A Day at the Beach",
    text:
      "Today is a hot day. Maria and her brother go to the beach. They swim in the sea. Maria builds a " +
      "sandcastle. Her brother collects shells. At noon, they eat sandwiches under an umbrella. In the " +
      "evening, they watch the sunset.",
    questions: [
      { q: "What is the weather like today?", options: ["Cold", "Hot", "Rainy", "Snowy"], correct: 1, hint: "Look at the first sentence.", explain: "The text says: \"Today is a hot day.\"" },
      { q: "Where do Maria and her brother go?", options: ["To the park", "To the beach", "To school", "To the mountains"], correct: 1, hint: "Look at the second sentence.", explain: "The text says: \"Maria and her brother go to the beach.\"" },
      { q: "What does Maria build?", options: ["A sandcastle", "A boat", "A tower", "A house"], correct: 0, hint: "Look at the sentence about Maria.", explain: "The text says: \"Maria builds a sandcastle.\"" },
      { q: "What does her brother collect?", options: ["Rocks", "Shells", "Leaves", "Coins"], correct: 1, hint: "Look at the sentence about her brother.", explain: "The text says: \"Her brother collects shells.\"" },
      { q: "What do they do in the evening?", options: ["They go home", "They watch the sunset", "They swim again", "They eat dinner"], correct: 1, hint: "Look at the last sentence.", explain: "The text says: \"In the evening, they watch the sunset.\"" },
      { q: "Who goes to the beach with Maria?", options: ["Her friend", "Her brother", "Her mother", "Her teacher"], correct: 1, hint: "Look at the second sentence.", explain: "The text says Maria and her brother go to the beach together." },
      { q: "What do Maria and her brother do in the sea?", options: ["They swim", "They fish", "They sail", "They dive"], correct: 0, hint: "Look at the sentence about the sea.", explain: "The text says they swim in the sea." },
      { q: "When do they eat sandwiches?", options: ["In the morning", "At noon", "In the evening", "At night"], correct: 1, hint: "Look at the sentence about eating.", explain: "The text says they eat sandwiches at noon." },
      { q: "Where do they eat their sandwiches?", options: ["On a boat", "Under an umbrella", "In a cafe", "At home"], correct: 1, hint: "Look at the sentence about eating.", explain: "The text says they eat under an umbrella." },
      { q: "How many people go to the beach in the story?", options: ["One", "Two", "Three", "Four"], correct: 1, hint: "Think about Maria and her brother.", explain: "The text mentions Maria and her brother, so two people go." },
    ],
  },
  {
    level: "A1",
    title: "My Favorite Animal",
    text:
      "My favorite animal is the elephant. Elephants are very big and gray. They have long trunks and big " +
      "ears. Elephants eat leaves, grass, and fruit. They live in Africa and Asia. Elephants are very smart " +
      "animals.",
    questions: [
      { q: "What is the writer's favorite animal?", options: ["The lion", "The elephant", "The tiger", "The monkey"], correct: 1, hint: "Look at the first sentence.", explain: "The text says: \"My favorite animal is the elephant.\"" },
      { q: "What color are elephants?", options: ["Brown", "Gray", "Black", "White"], correct: 1, hint: "Look at the second sentence.", explain: "The text says elephants are \"very big and gray.\"" },
      { q: "What do elephants have?", options: ["Long trunks and big ears", "Short tails and small eyes", "Wings and feathers", "Sharp teeth and claws"], correct: 0, hint: "Look at the third sentence.", explain: "The text says they \"have long trunks and big ears.\"" },
      { q: "What do elephants eat?", options: ["Meat and fish", "Leaves, grass, and fruit", "Only fruit", "Insects"], correct: 1, hint: "Look at the sentence about food.", explain: "The text says elephants \"eat leaves, grass, and fruit.\"" },
      { q: "Where do elephants live?", options: ["Only in Africa", "Africa and Asia", "Only in Asia", "Europe and Africa"], correct: 1, hint: "Look at the sentence about where they live.", explain: "The text says they \"live in Africa and Asia.\"" },
      { q: "How big are elephants, according to the text?", options: ["Very small", "Very big", "Medium-sized", "Tiny"], correct: 1, hint: "Look at the second sentence.", explain: "The text says elephants are very big and gray." },
      { q: "What are an elephant's ears like?", options: ["Small", "Big", "Pointed", "Round and tiny"], correct: 1, hint: "Look at the sentence about trunks and ears.", explain: "The text says elephants have long trunks and big ears." },
      { q: "Do elephants eat meat, according to the text?", options: ["Yes, only meat", "No, they eat leaves, grass, and fruit", "Yes, meat and fish", "Only insects"], correct: 1, hint: "Look at the sentence about food.", explain: "The text lists leaves, grass, and fruit, not meat." },
      { q: "What does the text say about elephants' intelligence?", options: ["They are not clever", "They are very smart", "They cannot learn", "The text does not say"], correct: 1, hint: "Look at the last sentence.", explain: "The text says elephants are very smart animals." },
      { q: "What long body part do elephants have on their face?", options: ["A trunk", "A horn", "A beak", "A mane"], correct: 0, hint: "Look at the sentence about trunks and ears.", explain: "The text says elephants have long trunks." },
    ],
  },

  // ===================== A2 =====================
  {
    level: "A2",
    title: "A Letter to a Friend",
    text:
      "Dear Anna, I hope you are doing well. Last month, I moved to a new apartment in the city center. My " +
      "new apartment is smaller than my old one, but it is much closer to my office. Now I can walk to work " +
      "instead of taking the bus. On weekends, I like to explore the local cafes and parks nearby. I hope " +
      "you can visit me soon. Best wishes, Laura",
    questions: [
      { q: "What did Laura do last month?", options: ["She got a new job", "She moved to a new apartment", "She went on vacation", "She started school"], correct: 1, hint: "Look at the second sentence.", explain: "The text says: \"Last month, I moved to a new apartment.\"" },
      { q: "How does the new apartment compare to the old one?", options: ["It is bigger", "It is smaller but closer to the office", "It is the same size", "It is farther from work"], correct: 1, hint: "Look at the sentence comparing the apartments.", explain: "The text says it is \"smaller than my old one, but...much closer to my office.\"" },
      { q: "How does Laura get to work now?", options: ["By bus", "By walking", "By car", "By bike"], correct: 1, hint: "Look at the sentence about getting to work.", explain: "The text says, \"Now I can walk to work instead of taking the bus.\"" },
      { q: "What does Laura like to do on weekends?", options: ["Stay home", "Explore cafes and parks", "Work extra hours", "Visit Anna"], correct: 1, hint: "Look at the sentence about weekends.", explain: "The text says she likes \"to explore the local cafes and parks nearby.\"" },
      { q: "What does Laura hope for at the end of the letter?", options: ["That Anna will move too", "That Anna can visit her soon", "That Anna will write back", "That Anna will find a new job"], correct: 1, hint: "Look at the last sentence before the signature.", explain: "The text says, \"I hope you can visit me soon.\"" },
      { q: "Who is Laura writing this letter to?", options: ["Her boss", "Anna", "Her sister", "A stranger"], correct: 1, hint: "Look at the greeting at the start.", explain: "The letter begins with Dear Anna." },
      { q: "Where is Laura's new apartment located?", options: ["In the countryside", "In the city center", "Near the beach", "In another country"], correct: 1, hint: "Look at the sentence about moving.", explain: "The text says she moved to a new apartment in the city center." },
      { q: "Why is the new apartment better for work?", options: ["It is bigger", "It is closer to her office", "It has a garden", "It is cheaper"], correct: 1, hint: "Look at the sentence comparing apartments.", explain: "The text says it is much closer to her office." },
      { q: "How did Laura travel to work before she moved?", options: ["By bus", "By car", "By train", "On foot"], correct: 0, hint: "Look at the sentence about walking to work now.", explain: "The text says she now walks instead of taking the bus, so she used the bus before." },
      { q: "Who wrote and signed the letter?", options: ["Anna", "Laura", "Tom", "Ella"], correct: 1, hint: "Look at the signature at the end.", explain: "The letter is signed Best wishes, Laura." },
    ],
  },
  {
    level: "A2",
    title: "Planning a Birthday Party",
    text:
      "Tom is planning a birthday party for his sister, Ella. The party will be next Saturday afternoon. He " +
      "wants to invite twenty guests. Tom is going to order a chocolate cake from the bakery downtown. He " +
      "also needs to buy balloons and decorations. His parents will help him cook the food. Ella does not " +
      "know about the party yet, so everyone must keep it a secret.",
    questions: [
      { q: "Who is the party for?", options: ["Tom", "Ella", "Their parents", "A friend"], correct: 1, hint: "Look at the first sentence.", explain: "The text says Tom is planning a party \"for his sister, Ella.\"" },
      { q: "When is the party?", options: ["Next Friday morning", "Next Saturday afternoon", "Next Sunday evening", "Tomorrow"], correct: 1, hint: "Look at the second sentence.", explain: "The text says, \"The party will be next Saturday afternoon.\"" },
      { q: "How many guests does Tom want to invite?", options: ["Ten", "Fifteen", "Twenty", "Thirty"], correct: 2, hint: "Look at the sentence about guests.", explain: "The text says, \"He wants to invite twenty guests.\"" },
      { q: "Where will Tom order the cake from?", options: ["A supermarket", "A bakery downtown", "A restaurant", "He will bake it himself"], correct: 1, hint: "Look at the sentence about the cake.", explain: "The text says he will order it \"from the bakery downtown.\"" },
      { q: "Does Ella know about the party?", options: ["Yes, she is helping to plan it", "No, it is a secret", "Yes, but she is pretending not to know", "The text does not say"], correct: 1, hint: "Look at the last sentence.", explain: "The text says, \"Ella does not know about the party yet, so everyone must keep it a secret.\"" },
      { q: "Who is planning the birthday party?", options: ["Ella", "Tom", "Their parents", "Anna"], correct: 1, hint: "Look at the first sentence.", explain: "The text says Tom is planning the party." },
      { q: "What kind of cake will Tom order?", options: ["A vanilla cake", "A chocolate cake", "A fruit cake", "A cheesecake"], correct: 1, hint: "Look at the sentence about the cake.", explain: "The text says Tom will order a chocolate cake." },
      { q: "What else does Tom need to buy for the party?", options: ["Chairs and tables", "Balloons and decorations", "Plates and cups", "Music and lights"], correct: 1, hint: "Look at the sentence after the cake.", explain: "The text says he needs to buy balloons and decorations." },
      { q: "Who will help Tom cook the food?", options: ["His friends", "His parents", "The bakery", "Ella"], correct: 1, hint: "Look at the sentence about food.", explain: "The text says his parents will help him cook the food." },
      { q: "What is Ella to Tom?", options: ["His sister", "His mother", "His friend", "His cousin"], correct: 0, hint: "Look at the first sentence.", explain: "The text says Ella is Tom's sister." },
    ],
  },

  // ===================== B1 =====================
  {
    level: "B1",
    title: "Learning a New Language",
    text:
      "Learning a new language can open many doors, both personally and professionally. Many people start by " +
      "learning basic vocabulary and grammar rules, but real fluency often comes from practicing with native " +
      "speakers. Watching movies and listening to podcasts in the target language can also improve listening " +
      "skills significantly. Some learners find it helpful to keep a journal in the new language, writing a " +
      "few sentences every day. Although mistakes are a normal part of the learning process, many beginners " +
      "feel embarrassed to speak until they are more confident. Experts agree, however, that the fastest way " +
      "to improve is to practice speaking as often as possible, even if it feels uncomfortable at first.",
    questions: [
      { q: "What can learning a new language open, according to the text?", options: ["Only professional doors", "Many doors, both personally and professionally", "Only personal doors", "No real opportunities"], correct: 1, hint: "Look at the first sentence.", explain: "The text says it \"can open many doors, both personally and professionally.\"" },
      { q: "Where does real fluency often come from, according to the text?", options: ["Reading grammar books", "Practicing with native speakers", "Watching TV only", "Taking written tests"], correct: 1, hint: "Look at the second sentence.", explain: "The text says fluency \"often comes from practicing with native speakers.\"" },
      { q: "What can improve listening skills, according to the text?", options: ["Writing essays", "Watching movies and listening to podcasts", "Doing grammar exercises", "Memorizing word lists"], correct: 1, hint: "Look at the sentence about listening skills.", explain: "The text says these activities \"can also improve listening skills significantly.\"" },
      { q: "Why do many beginners feel embarrassed to speak?", options: ["They dislike the language", "Mistakes are normal, but it still feels uncomfortable until they are confident", "They do not want to learn", "Their teachers discourage them"], correct: 1, hint: "Look at the sentence about mistakes.", explain: "The text says \"many beginners feel embarrassed to speak until they are more confident,\" even though mistakes are normal." },
      { q: "What do experts say is the fastest way to improve, according to the text?", options: ["Studying grammar alone", "Practicing speaking as often as possible", "Avoiding mistakes completely", "Only reading books"], correct: 1, hint: "Look at the last sentence.", explain: "The text says experts agree \"the fastest way to improve is to practice speaking as often as possible.\"" },
      { q: "How do many people begin learning a new language, according to the text?", options: ["By moving abroad", "By learning basic vocabulary and grammar rules", "By taking exams", "By teaching others"], correct: 1, hint: "Look at the second sentence.", explain: "The text says many people start with basic vocabulary and grammar rules." },
      { q: "What do some learners keep in the new language?", options: ["A dictionary", "A journal", "A textbook", "A recording"], correct: 1, hint: "Look at the sentence about writing daily.", explain: "The text says some learners keep a journal in the new language." },
      { q: "How much do journal-keepers write each day, according to the text?", options: ["A full page", "A few sentences", "Nothing", "An entire story"], correct: 1, hint: "Look at the sentence about the journal.", explain: "The text says they write a few sentences every day." },
      { q: "How does the text describe mistakes in learning?", options: ["As a sign of failure", "As a normal part of the process", "As something to avoid completely", "As unimportant"], correct: 1, hint: "Look at the sentence about mistakes.", explain: "The text says mistakes are a normal part of the learning process." },
      { q: "According to the text, how can watching movies and podcasts help?", options: ["They improve listening skills", "They replace speaking practice", "They teach grammar rules", "They are not useful"], correct: 0, hint: "Look at the sentence about movies and podcasts.", explain: "The text says they can improve listening skills significantly." },
    ],
  },
  {
    level: "B1",
    title: "The Rise of Remote Work",
    text:
      "In recent years, more and more companies have allowed employees to work from home rather than " +
      "commuting to an office every day. This shift became especially common after many businesses were " +
      "forced to adapt during a global health crisis. Employees often report that remote work gives them " +
      "more flexibility and helps them balance their personal and professional lives. However, some workers " +
      "say they miss the social interaction of an office environment and find it harder to separate work " +
      "time from personal time. Managers have also had to find new ways to keep teams connected, often " +
      "relying on video calls and messaging apps. As a result, many companies now offer a hybrid model, " +
      "allowing staff to split their time between the office and home.",
    questions: [
      { q: "What have more companies allowed employees to do in recent years?", options: ["Take longer vacations", "Work from home", "Work fewer hours", "Change careers easily"], correct: 1, hint: "Look at the first sentence.", explain: "The text says companies have allowed employees \"to work from home rather than commuting to an office every day.\"" },
      { q: "When did this shift become especially common?", options: ["During a global health crisis", "Before the internet existed", "During a recession", "After a war"], correct: 0, hint: "Look at the second sentence.", explain: "The text says it became common \"after many businesses were forced to adapt during a global health crisis.\"" },
      { q: "What benefit do employees often report from remote work?", options: ["Higher salaries", "More flexibility and better work-life balance", "Faster promotions", "More vacation days"], correct: 1, hint: "Look at the sentence about employees' reports.", explain: "The text says it \"gives them more flexibility and helps them balance their personal and professional lives.\"" },
      { q: "What do some workers say they miss about the office?", options: ["The commute", "Social interaction", "Their desks", "The parking lot"], correct: 1, hint: "Look at the sentence starting with 'However'.", explain: "The text says some workers \"miss the social interaction of an office environment.\"" },
      { q: "What model do many companies now offer, according to the text?", options: ["Fully remote only", "A hybrid model splitting time between office and home", "Fully in-office only", "A four-day work week"], correct: 1, hint: "Look at the last sentence.", explain: "The text says many companies \"now offer a hybrid model, allowing staff to split their time between the office and home.\"" },
      { q: "What is the traditional alternative to remote work mentioned in the text?", options: ["Working night shifts", "Commuting to an office every day", "Working part-time", "Freelancing"], correct: 1, hint: "Look at the first sentence.", explain: "The text contrasts remote work with commuting to an office every day." },
      { q: "Besides missing social interaction, what else do some workers find harder?", options: ["Finishing tasks", "Separating work time from personal time", "Using computers", "Talking to clients"], correct: 1, hint: "Look at the sentence starting with However.", explain: "The text says some workers find it harder to separate work time from personal time." },
      { q: "What tools do managers rely on to keep teams connected?", options: ["Letters and fax", "Video calls and messaging apps", "In-person meetings only", "Newspapers"], correct: 1, hint: "Look at the sentence about managers.", explain: "The text says managers often rely on video calls and messaging apps." },
      { q: "What does a hybrid model allow staff to do?", options: ["Work only at night", "Split their time between the office and home", "Never come to the office", "Work fewer days"], correct: 1, hint: "Look at the last sentence.", explain: "The text says a hybrid model lets staff split time between office and home." },
      { q: "According to the text, why did remote work become common?", options: ["Because offices closed forever", "Because businesses were forced to adapt during a global health crisis", "Because employees demanded it", "Because it was cheaper"], correct: 1, hint: "Look at the second sentence.", explain: "The text says it became common after businesses adapted during a global health crisis." },
    ],
  },

  // ===================== B2 =====================
  {
    level: "B2",
    title: "The Psychology of Procrastination",
    text:
      "Procrastination is often misunderstood as simple laziness, but psychologists argue that it is actually " +
      "a much more complex issue rooted in emotional regulation rather than time management. When faced with " +
      "a task that provokes anxiety, boredom, or self-doubt, the brain seeks short-term relief by turning to " +
      "more pleasant activities, even when the person is fully aware of the negative long-term consequences. " +
      "This explains why simply telling someone to 'just do it' rarely solves the underlying problem. " +
      "Research suggests that breaking a large task into smaller, more manageable steps can reduce the " +
      "emotional burden associated with it, making it easier to begin. Additionally, forgiving oneself for " +
      "past procrastination, rather than dwelling on guilt, has been shown to reduce the likelihood of " +
      "procrastinating again in the future. Understanding procrastination as an emotional coping mechanism, " +
      "rather than a character flaw, may therefore be the first step toward overcoming it.",
    questions: [
      { q: "How do psychologists describe procrastination, according to the text?", options: ["As simple laziness", "As a complex issue rooted in emotional regulation", "As a rare personality disorder", "As something that cannot be studied"], correct: 1, hint: "Look at the first sentence.", explain: "The text says it is \"a much more complex issue rooted in emotional regulation rather than time management.\"" },
      { q: "Why does the brain turn to more pleasant activities, according to the text?", options: ["It seeks short-term relief from anxiety, boredom, or self-doubt", "It cannot process difficult tasks", "It is programmed to avoid all work", "It prefers physical activities"], correct: 0, hint: "Look at the sentence about the brain seeking relief.", explain: "The text says the brain \"seeks short-term relief by turning to more pleasant activities.\"" },
      { q: "Why does simply telling someone to 'just do it' rarely work, according to the text?", options: ["It does not address the underlying emotional problem", "People do not understand the instruction", "It is too demanding", "It works most of the time"], correct: 0, hint: "Look at the sentence about 'just do it'.", explain: "The text says it \"rarely solves the underlying problem,\" since procrastination is emotional, not just about willpower." },
      { q: "What does research suggest can reduce the emotional burden of a task?", options: ["Ignoring the task completely", "Breaking it into smaller, manageable steps", "Working longer hours", "Asking someone else to do it"], correct: 1, hint: "Look at the sentence about research.", explain: "The text says breaking tasks into smaller steps \"can reduce the emotional burden associated with it.\"" },
      { q: "What has been shown to reduce future procrastination, according to the text?", options: ["Feeling guilty about past procrastination", "Forgiving oneself for past procrastination", "Punishing oneself strictly", "Avoiding all deadlines"], correct: 1, hint: "Look at the sentence about forgiving oneself.", explain: "The text says \"forgiving oneself for past procrastination...has been shown to reduce the likelihood of procrastinating again.\"" },
      { q: "What is procrastination often mistaken for, according to the text?", options: ["A medical illness", "Simple laziness", "A learning disability", "A time-management skill"], correct: 1, hint: "Look at the first sentence.", explain: "The text says procrastination is often misunderstood as simple laziness." },
      { q: "What feelings can a task provoke that lead to procrastination?", options: ["Joy and excitement", "Anxiety, boredom, or self-doubt", "Hunger and thirst", "Pride and confidence"], correct: 1, hint: "Look at the sentence about the brain.", explain: "The text lists anxiety, boredom, or self-doubt." },
      { q: "Is a person usually unaware of the long-term consequences when procrastinating?", options: ["Yes, completely unaware", "No, they are often fully aware", "The text does not say", "Only sometimes"], correct: 1, hint: "Look at the sentence about pleasant activities.", explain: "The text says people are fully aware of the negative long-term consequences." },
      { q: "Instead of dwelling on guilt, what does the text suggest doing?", options: ["Punishing yourself", "Forgiving yourself", "Ignoring the task", "Blaming others"], correct: 1, hint: "Look at the sentence about forgiving.", explain: "The text says forgiving oneself reduces future procrastination." },
      { q: "The text suggests seeing procrastination as what, rather than a character flaw?", options: ["An emotional coping mechanism", "A sign of laziness", "A permanent habit", "A physical illness"], correct: 0, hint: "Look at the last sentence.", explain: "The text calls it an emotional coping mechanism rather than a character flaw." },
    ],
  },
  {
    level: "B2",
    title: "The Impact of Social Media on Attention Spans",
    text:
      "Over the past decade, researchers have raised concerns about the effect of social media platforms on " +
      "human attention spans. These platforms are often designed with features such as infinite scrolling " +
      "and instant notifications, which are specifically engineered to capture and hold users' attention for " +
      "as long as possible. Some studies suggest that frequent use of such platforms may make it more " +
      "difficult for individuals to concentrate on longer, less stimulating tasks, such as reading a book or " +
      "completing detailed work assignments. Critics of this view, however, point out that attention spans " +
      "have always varied depending on the task and the individual's interest level, and that blaming " +
      "technology alone oversimplifies a complicated issue. Regardless of the exact cause, many experts " +
      "recommend setting specific boundaries around screen time, such as disabling notifications during work " +
      "hours, as a practical way to protect one's ability to focus.",
    questions: [
      { q: "What have researchers raised concerns about over the past decade?", options: ["The cost of smartphones", "The effect of social media on attention spans", "The speed of the internet", "The number of social media users"], correct: 1, hint: "Look at the first sentence.", explain: "The text says researchers are concerned about \"the effect of social media platforms on human attention spans.\"" },
      { q: "What are features like infinite scrolling designed to do?", options: ["Save battery life", "Capture and hold users' attention", "Reduce data usage", "Improve video quality"], correct: 1, hint: "Look at the second sentence.", explain: "The text says these features are \"specifically engineered to capture and hold users' attention.\"" },
      { q: "What do some studies suggest frequent social media use may cause?", options: ["Improved reading skills", "Difficulty concentrating on longer, less stimulating tasks", "Better memory", "Faster typing speed"], correct: 1, hint: "Look at the sentence about studies.", explain: "The text says it \"may make it more difficult for individuals to concentrate on longer, less stimulating tasks.\"" },
      { q: "What do critics argue, according to the text?", options: ["Technology is the only cause of short attention spans", "Attention spans have always varied by task and interest level", "Social media has no effect at all", "Everyone should stop using social media"], correct: 1, hint: "Look at the sentence starting with 'Critics'.", explain: "The text says critics point out \"attention spans have always varied depending on the task and the individual's interest level.\"" },
      { q: "What do many experts recommend, according to the text?", options: ["Banning social media completely", "Setting boundaries around screen time", "Ignoring the issue", "Using social media more often"], correct: 1, hint: "Look at the last sentence.", explain: "The text says experts recommend \"setting specific boundaries around screen time...to protect one's ability to focus.\"" },
      { q: "Name one design feature these platforms use, according to the text.", options: ["Slow loading", "Instant notifications", "Text-only pages", "Hourly reminders"], correct: 1, hint: "Look at the second sentence.", explain: "The text mentions infinite scrolling and instant notifications." },
      { q: "What kind of tasks may become harder to concentrate on?", options: ["Short, exciting tasks", "Longer, less stimulating tasks", "Physical exercise", "Simple games"], correct: 1, hint: "Look at the sentence about studies.", explain: "The text says longer, less stimulating tasks like reading a book." },
      { q: "According to critics, does blaming technology alone capture the issue?", options: ["Yes, fully", "No, it oversimplifies a complicated issue", "The text does not say", "Only for children"], correct: 1, hint: "Look at the sentence about critics.", explain: "The text says blaming technology alone oversimplifies a complicated issue." },
      { q: "What specific boundary do experts suggest?", options: ["Turning off the internet", "Disabling notifications during work hours", "Never using social media", "Deleting all apps"], correct: 1, hint: "Look at the last sentence.", explain: "The text suggests disabling notifications during work hours." },
      { q: "What is the goal of setting boundaries around screen time?", options: ["To save money", "To protect one's ability to focus", "To use more data", "To make more friends"], correct: 1, hint: "Look at the last sentence.", explain: "The text says it is a practical way to protect one's ability to focus." },
    ],
  },

  // ===================== C1 =====================
  {
    level: "C1",
    title: "The Ethics of Genetic Engineering",
    text:
      "Advances in genetic engineering, particularly the development of gene-editing tools such as CRISPR, " +
      "have opened unprecedented possibilities for treating hereditary diseases at their source rather than " +
      "merely managing symptoms. Proponents argue that the ability to correct harmful mutations before birth " +
      "could eliminate suffering caused by conditions that have plagued families for generations. " +
      "Nevertheless, this same technology raises profound ethical questions that extend well beyond medical " +
      "treatment. Critics warn that once genetic modification becomes feasible for eliminating disease, the " +
      "line between therapy and enhancement may blur, potentially opening the door to so-called 'designer " +
      "babies' selected for traits such as height, intelligence, or appearance. Such possibilities raise " +
      "concerns about exacerbating existing social inequalities, as access to these technologies would " +
      "likely be limited to those who can afford them. Furthermore, questions remain about the long-term, " +
      "unintended consequences of altering the human genome in ways that could be passed down to future " +
      "generations who never consented to the change. Consequently, many bioethicists argue that robust " +
      "international regulation, rather than an outright ban or unrestricted freedom, represents the most " +
      "prudent path forward.",
    questions: [
      { q: "What has the development of gene-editing tools like CRISPR opened, according to the text?", options: ["Unprecedented possibilities for treating hereditary diseases at their source", "New ways to manage symptoms only", "No real medical benefits", "Only theoretical possibilities with no practical use"], correct: 0, hint: "Look at the first sentence.", explain: "The text says it opened \"unprecedented possibilities for treating hereditary diseases at their source rather than merely managing symptoms.\"" },
      { q: "What do critics warn could happen once genetic modification becomes feasible for disease?", options: ["Nothing would change", "The line between therapy and enhancement may blur", "All diseases would be cured immediately", "Genetic engineering would become illegal"], correct: 1, hint: "Look at the sentence about critics.", explain: "The text says critics warn \"the line between therapy and enhancement may blur,\" opening the door to 'designer babies.'" },
      { q: "What social concern is raised by the possibility of 'designer babies'?", options: ["It could exacerbate existing social inequalities", "It would have no social impact", "It would make healthcare cheaper for everyone", "It would only affect wealthy countries positively"], correct: 0, hint: "Look at the sentence about social inequalities.", explain: "The text says this \"raise[s] concerns about exacerbating existing social inequalities, as access...would likely be limited to those who can afford them.\"" },
      { q: "What long-term question does the text raise about altering the human genome?", options: ["Whether it can be done quickly", "Whether future generations who never consented could be affected", "Whether it is expensive", "Whether scientists are capable of doing it"], correct: 1, hint: "Look at the sentence about future generations.", explain: "The text raises concerns about \"unintended consequences...that could be passed down to future generations who never consented to the change.\"" },
      { q: "What do many bioethicists argue is the most prudent path forward, according to the text?", options: ["An outright ban on all genetic engineering", "Unrestricted freedom with no rules", "Robust international regulation", "Leaving the decision only to individual scientists"], correct: 2, hint: "Look at the last sentence.", explain: "The text says bioethicists argue \"robust international regulation...represents the most prudent path forward.\"" },
      { q: "What do proponents argue correcting harmful mutations before birth could do?", options: ["Make people taller", "Eliminate suffering from conditions that have plagued families for generations", "Lower the cost of medicine", "Speed up evolution"], correct: 1, hint: "Look at the sentence about proponents.", explain: "The text says it could eliminate suffering from long-standing hereditary conditions." },
      { q: "Which traits are mentioned as possible 'designer baby' selections?", options: ["Height, intelligence, or appearance", "Blood type only", "Language and accent", "Personality and mood"], correct: 0, hint: "Look at the sentence about designer babies.", explain: "The text lists height, intelligence, or appearance." },
      { q: "Why might access to these technologies deepen inequality?", options: ["It would be free for everyone", "It would likely be limited to those who can afford it", "It would be banned everywhere", "It would only help poor families"], correct: 1, hint: "Look at the sentence about inequalities.", explain: "The text says access would likely be limited to those who can afford it." },
      { q: "Do the ethical questions stay within medical treatment, according to the text?", options: ["Yes, entirely", "No, they extend well beyond it", "The text does not say", "Only in some countries"], correct: 1, hint: "Look at the sentence starting with Nevertheless.", explain: "The text says the questions extend well beyond medical treatment." },
      { q: "Which two extremes do bioethicists reject in favor of regulation?", options: ["Science and religion", "An outright ban and unrestricted freedom", "Public and private funding", "Speed and safety"], correct: 1, hint: "Look at the last sentence.", explain: "The text contrasts robust regulation with an outright ban or unrestricted freedom." },
    ],
  },
  {
    level: "C1",
    title: "The Paradox of Choice in Consumer Culture",
    text:
      "Contemporary consumer culture is often characterized by an abundance of choice, with supermarket " +
      "shelves stocked with dozens of variations of a single product and online retailers offering seemingly " +
      "limitless options at the click of a button. Conventional economic theory has long held that greater " +
      "choice inherently benefits consumers by allowing them to select the option that best satisfies their " +
      "individual preferences. However, psychological research complicates this assumption considerably. " +
      "Studies have demonstrated that when confronted with an overwhelming number of options, individuals " +
      "frequently experience heightened anxiety, decision paralysis, and, paradoxically, diminished " +
      "satisfaction with whatever choice they ultimately make. This phenomenon, often termed the 'paradox of " +
      "choice,' suggests that beyond a certain threshold, additional options cease to be beneficial and " +
      "instead impose a cognitive burden that outweighs any marginal gain in personalization. Some " +
      "researchers propose that curated selections, in which a smaller number of carefully chosen options " +
      "are presented, may in fact produce greater consumer satisfaction than an exhaustive array of " +
      "alternatives. This insight has significant implications for retailers, policymakers, and designers of " +
      "any system in which humans must make decisions among multiple alternatives.",
    questions: [
      { q: "What has conventional economic theory long held about choice?", options: ["Greater choice inherently benefits consumers", "Choice has no effect on consumers", "Fewer choices are always better", "Consumers dislike having options"], correct: 0, hint: "Look at the second sentence.", explain: "The text says conventional theory held \"that greater choice inherently benefits consumers by allowing them to select the option that best satisfies their individual preferences.\"" },
      { q: "What have psychological studies demonstrated about too many options?", options: ["They always increase satisfaction", "They can cause anxiety, decision paralysis, and diminished satisfaction", "They have no psychological effect", "They only affect wealthy consumers"], correct: 1, hint: "Look at the sentence about studies.", explain: "The text says people \"frequently experience heightened anxiety, decision paralysis, and...diminished satisfaction with whatever choice they ultimately make.\"" },
      { q: "What is the 'paradox of choice', according to the text?", options: ["The idea that more choice always helps", "The idea that beyond a threshold, more options impose a burden rather than a benefit", "A marketing strategy used by retailers", "A term for having no choices at all"], correct: 1, hint: "Look at the sentence naming the paradox.", explain: "The text says beyond a threshold, \"additional options cease to be beneficial and instead impose a cognitive burden.\"" },
      { q: "What do some researchers propose as an alternative to an exhaustive array of choices?", options: ["Removing all choices entirely", "Curated selections with fewer, carefully chosen options", "Randomly assigning products to consumers", "Doubling the number of available options"], correct: 1, hint: "Look at the sentence about curated selections.", explain: "The text says curated selections \"may in fact produce greater consumer satisfaction than an exhaustive array of alternatives.\"" },
      { q: "Who does this insight have significant implications for, according to the last sentence?", options: ["Only supermarket owners", "Retailers, policymakers, and designers of decision-making systems", "Only online shoppers", "Only psychologists"], correct: 1, hint: "Look at the last sentence.", explain: "The text says it has implications for \"retailers, policymakers, and designers of any system in which humans must make decisions among multiple alternatives.\"" },
      { q: "How is contemporary consumer culture characterized, according to the text?", options: ["By a lack of products", "By an abundance of choice", "By rising prices", "By poor quality"], correct: 1, hint: "Look at the first sentence.", explain: "The text says it is characterized by an abundance of choice." },
      { q: "What does psychological research do to the conventional assumption about choice?", options: ["Confirms it fully", "Complicates it considerably", "Ignores it", "Proves it beyond doubt"], correct: 1, hint: "Look at the sentence about psychological research.", explain: "The text says psychological research complicates the assumption considerably." },
      { q: "Beyond a certain threshold, what do additional options impose?", options: ["A cognitive burden", "Lower prices", "Better quality", "More free time"], correct: 0, hint: "Look at the sentence naming the paradox.", explain: "The text says additional options impose a cognitive burden." },
      { q: "What kind of selections might produce greater satisfaction?", options: ["Endless options", "Curated selections with fewer, carefully chosen options", "Random options", "The cheapest options"], correct: 1, hint: "Look at the sentence about researchers.", explain: "The text says curated selections may produce greater satisfaction." },
      { q: "Does the cognitive burden outweigh the gain in personalization, according to the text?", options: ["Yes, beyond a threshold", "No, never", "Only for experts", "The text does not say"], correct: 0, hint: "Look at the sentence naming the paradox.", explain: "The text says the burden outweighs any marginal gain in personalization beyond a threshold." },
    ],
  },

  // ===================== C2 =====================
  {
    level: "C2",
    title: "Epistemic Humility in an Age of Information Abundance",
    text:
      "Paradoxically, the unprecedented accessibility of information in the digital age has not necessarily " +
      "translated into a correspondingly well-informed populace; if anything, the sheer volume of competing " +
      "claims, often stripped of the contextual scaffolding that once accompanied traditional gatekept " +
      "media, has rendered the task of discerning reliable knowledge from misinformation considerably more " +
      "fraught. Philosophers of knowledge have long emphasized the virtue of epistemic humility -- the " +
      "disposition to recognize the limits of one's own understanding and to remain appropriately tentative " +
      "in the face of uncertainty -- as a crucial safeguard against both credulity and unwarranted " +
      "skepticism. Yet cultivating this disposition proves markedly more difficult within algorithmically " +
      "curated information environments, which tend to reward engagement over accuracy and often insulate " +
      "users within ideologically congenial echo chambers. Some scholars contend that addressing this " +
      "predicament requires not merely improved media literacy education, but a broader cultural " +
      "reorientation that valorizes intellectual humility and productive uncertainty over the confident " +
      "assertion that so often garners social capital in public discourse. Absent such a shift, they argue, " +
      "the abundance of information may continue to coexist uneasily with a genuine impoverishment of " +
      "collective understanding.",
    questions: [
      { q: "What paradox does the text identify regarding the digital age?", options: ["More information has led to a better-informed populace", "Unprecedented access to information has not necessarily led to a well-informed populace", "Information is now scarce", "Traditional media provided no context at all"], correct: 1, hint: "Look at the first sentence.", explain: "The text says accessibility of information \"has not necessarily translated into a correspondingly well-informed populace.\"" },
      { q: "What is 'epistemic humility', as described in the text?", options: ["Recognizing the limits of one's own understanding and remaining tentative amid uncertainty", "Refusing to ever change one's mind", "Complete confidence in one's own knowledge", "A term for ignoring all information"], correct: 0, hint: "Look at the sentence defining epistemic humility.", explain: "The text defines it as \"the disposition to recognize the limits of one's own understanding and to remain appropriately tentative in the face of uncertainty.\"" },
      { q: "Why is cultivating epistemic humility harder in algorithmically curated environments, according to the text?", options: ["These environments reward engagement over accuracy and create echo chambers", "These environments have no algorithms at all", "These environments always promote accurate information", "These environments require no user engagement"], correct: 0, hint: "Look at the sentence about algorithmically curated environments.", explain: "The text says such environments \"tend to reward engagement over accuracy and often insulate users within ideologically congenial echo chambers.\"" },
      { q: "What do some scholars argue is needed beyond improved media literacy education?", options: ["Nothing more is needed", "A broader cultural reorientation valuing intellectual humility over confident assertion", "Banning algorithmic curation entirely", "More confident public discourse"], correct: 1, hint: "Look at the sentence about scholars' proposals.", explain: "The text says they argue for \"a broader cultural reorientation that valorizes intellectual humility and productive uncertainty over...confident assertion.\"" },
      { q: "What do scholars argue may continue without such a cultural shift, according to the last sentence?", options: ["A perfect balance of information and understanding", "Information abundance coexisting with an impoverishment of collective understanding", "A complete end to misinformation", "Universal epistemic humility"], correct: 1, hint: "Look at the last sentence.", explain: "The text says information abundance \"may continue to coexist uneasily with a genuine impoverishment of collective understanding.\"" },
      { q: "What has made discerning reliable knowledge more difficult, according to the text?", options: ["Too little information", "The sheer volume of competing claims stripped of context", "Slow internet", "A lack of writers"], correct: 1, hint: "Look at the first sentence.", explain: "The text blames the sheer volume of competing claims stripped of context." },
      { q: "Epistemic humility guards against which two extremes?", options: ["Wealth and poverty", "Credulity and unwarranted skepticism", "Speed and delay", "Fame and obscurity"], correct: 1, hint: "Look at the sentence defining epistemic humility.", explain: "The text says it guards against both credulity and unwarranted skepticism." },
      { q: "What do algorithmically curated environments tend to reward?", options: ["Accuracy over engagement", "Engagement over accuracy", "Silence over speech", "Length over brevity"], correct: 1, hint: "Look at the sentence about environments.", explain: "The text says they reward engagement over accuracy." },
      { q: "What do such environments insulate users within?", options: ["Ideologically congenial echo chambers", "Public libraries", "Neutral debates", "Fact-checked feeds"], correct: 0, hint: "Look at the sentence about environments.", explain: "The text says they insulate users within ideologically congenial echo chambers." },
      { q: "Is improved media literacy education alone enough, according to some scholars?", options: ["Yes, it solves everything", "No, a broader cultural reorientation is also needed", "The text does not say", "Only for students"], correct: 1, hint: "Look at the sentence about scholars.", explain: "The text says a broader cultural reorientation is also needed." },
    ],
  },
  {
    level: "C2",
    title: "The Limits of Rational Choice Theory",
    text:
      "Rational choice theory, long a cornerstone of economic and political analysis, posits that individuals " +
      "act to maximize their own utility based on a coherent and stable set of preferences, weighing costs " +
      "and benefits with something approaching computational precision. Behavioral economists, however, " +
      "have amassed a substantial body of empirical evidence suggesting that human decision-making deviates " +
      "systematically -- rather than randomly -- from this idealized model. Cognitive biases such as loss " +
      "aversion, wherein the pain of losing something is felt more acutely than the pleasure of an " +
      "equivalent gain, and present bias, which inclines individuals to overvalue immediate rewards relative " +
      "to future ones, recur with such regularity across cultures and contexts that they can scarcely be " +
      "dismissed as mere noise or measurement error. Proponents of rational choice theory have responded not " +
      "by abandoning the framework wholesale, but by incorporating these behavioral insights into more " +
      "nuanced models that retain optimization as a baseline while accounting for predictable deviations " +
      "from it. Critics, meanwhile, contend that such amendments risk rendering the theory unfalsifiable, " +
      "capable of explaining any observed behavior after the fact by simply invoking an appropriately " +
      "tailored bias. This ongoing tension between explanatory parsimony and empirical fidelity remains a " +
      "defining feature of contemporary debates in behavioral science.",
    questions: [
      { q: "What does rational choice theory posit, according to the text?", options: ["Individuals act randomly with no clear preferences", "Individuals act to maximize utility based on coherent, stable preferences", "Individuals never weigh costs and benefits", "Preferences constantly change with no pattern"], correct: 1, hint: "Look at the first sentence.", explain: "The text says it posits individuals \"act to maximize their own utility based on a coherent and stable set of preferences.\"" },
      { q: "How do behavioral economists say human decision-making deviates from the idealized model?", options: ["Randomly and unpredictably", "Systematically rather than randomly", "It does not deviate at all", "Only in rare, isolated cases"], correct: 1, hint: "Look at the second sentence.", explain: "The text says decision-making \"deviates systematically -- rather than randomly -- from this idealized model.\"" },
      { q: "What is 'loss aversion', as described in the text?", options: ["Feeling the pain of a loss more acutely than the pleasure of an equivalent gain", "Preferring losses over gains", "Being indifferent to both losses and gains", "Only caring about future rewards"], correct: 0, hint: "Look at the sentence defining loss aversion.", explain: "The text defines it as when \"the pain of losing something is felt more acutely than the pleasure of an equivalent gain.\"" },
      { q: "How have proponents of rational choice theory responded to behavioral evidence, according to the text?", options: ["By abandoning the framework entirely", "By incorporating behavioral insights into more nuanced models", "By ignoring all criticism", "By rejecting the idea of optimization completely"], correct: 1, hint: "Look at the sentence about proponents' response.", explain: "The text says they responded \"by incorporating these behavioral insights into more nuanced models that retain optimization as a baseline.\"" },
      { q: "What do critics argue such amendments risk, according to the text?", options: ["Making the theory more falsifiable", "Rendering the theory unfalsifiable", "Making the theory simpler", "Eliminating all bias from human behavior"], correct: 1, hint: "Look at the sentence about critics.", explain: "The text says critics argue the amendments \"risk rendering the theory unfalsifiable, capable of explaining any observed behavior after the fact.\"" },
      { q: "What has rational choice theory long been, according to the text?", options: ["A minor idea", "A cornerstone of economic and political analysis", "A recent discovery", "A rejected theory"], correct: 1, hint: "Look at the first sentence.", explain: "The text calls it a cornerstone of economic and political analysis." },
      { q: "What is 'present bias', as described in the text?", options: ["Overvaluing immediate rewards relative to future ones", "Preferring the past", "Ignoring all rewards", "Valuing the future more"], correct: 0, hint: "Look at the sentence about biases.", explain: "The text defines present bias as overvaluing immediate rewards relative to future ones." },
      { q: "Why can't these biases be dismissed as mere noise?", options: ["They are rare", "They recur with regularity across cultures and contexts", "They only affect experts", "They cannot be measured"], correct: 1, hint: "Look at the sentence about recurring biases.", explain: "The text says they recur with such regularity that they cannot be dismissed as noise." },
      { q: "In the nuanced models, what do proponents keep as a baseline?", options: ["Randomness", "Optimization", "Emotion", "Guesswork"], correct: 1, hint: "Look at the sentence about proponents.", explain: "The text says they retain optimization as a baseline." },
      { q: "What ongoing tension does the text say defines behavioral science debates?", options: ["Cost versus profit", "Explanatory parsimony versus empirical fidelity", "Speed versus accuracy", "Theory versus practice"], correct: 1, hint: "Look at the last sentence.", explain: "The text names the tension between explanatory parsimony and empirical fidelity." },
    ],
  },
  {
    level: "A1",
    title: "My School Day",
    text:
      "I go to school every morning. My school is big. My favorite subject is math. I have lunch at twelve o'clock. In the afternoon, I have English class. My teacher is very kind. After school, I walk home with my friend. In the evening, I do my homework.",
    questions: [
      { q: "When does the writer go to school?", options: ["At night", "Every morning", "On weekends", "At noon"], correct: 1, hint: "Look at the first sentence.", explain: "The text says the writer goes to school every morning." },
      { q: "How is the school described?", options: ["Small", "Big", "Old", "New"], correct: 1, hint: "Look at the second sentence.", explain: "The text says the school is big." },
      { q: "What is the writer's favorite subject?", options: ["English", "Math", "Art", "Music"], correct: 1, hint: "Look at the third sentence.", explain: "The text says the favorite subject is math." },
      { q: "When does the writer have lunch?", options: ["At ten o'clock", "At twelve o'clock", "At two o'clock", "In the evening"], correct: 1, hint: "Look at the sentence about lunch.", explain: "The text says lunch is at twelve o'clock." },
      { q: "What class does the writer have in the afternoon?", options: ["Math", "English", "Science", "Sport"], correct: 1, hint: "Look at the sentence about the afternoon.", explain: "The text says the writer has English class in the afternoon." },
      { q: "How is the teacher?", options: ["Very strict", "Very kind", "Very tired", "Very loud"], correct: 1, hint: "Look at the sentence about the teacher.", explain: "The text says the teacher is very kind." },
      { q: "Who does the writer walk home with?", options: ["A brother", "A friend", "A teacher", "No one"], correct: 1, hint: "Look at the sentence about after school.", explain: "The text says the writer walks home with a friend." },
      { q: "When does the writer do homework?", options: ["In the morning", "At lunch", "In the afternoon", "In the evening"], correct: 3, hint: "Look at the last sentence.", explain: "The text says the writer does homework in the evening." },
      { q: "How does the writer get home after school?", options: ["By bus", "By walking", "By car", "By bike"], correct: 1, hint: "Look at the sentence about after school.", explain: "The text says the writer walks home." },
      { q: "Does the writer like math?", options: ["No", "Yes, it is the favorite subject", "The text does not say", "Only a little"], correct: 1, hint: "Look at the sentence about the favorite subject.", explain: "Math is the writer's favorite subject, so yes." },
    ],
  },
  {
    level: "A1",
    title: "The Little Garden",
    text:
      "My grandmother has a small garden. In the garden, there are red roses and yellow flowers. She grows tomatoes and carrots. A little bird lives in the tree. Every morning, my grandmother waters the plants. In summer, we eat vegetables from the garden. I love the garden very much.",
    questions: [
      { q: "Who has the garden?", options: ["The writer", "The grandmother", "A friend", "The neighbor"], correct: 1, hint: "Look at the first sentence.", explain: "The text says the writer's grandmother has the garden." },
      { q: "How big is the garden?", options: ["Very big", "Small", "Huge", "Medium"], correct: 1, hint: "Look at the first sentence.", explain: "The text says the garden is small." },
      { q: "What color are the roses?", options: ["Red", "Yellow", "White", "Pink"], correct: 0, hint: "Look at the second sentence.", explain: "The text says there are red roses." },
      { q: "What vegetables grow in the garden?", options: ["Potatoes and beans", "Tomatoes and carrots", "Onions and peas", "Corn and cabbage"], correct: 1, hint: "Look at the third sentence.", explain: "The text says she grows tomatoes and carrots." },
      { q: "What lives in the tree?", options: ["A cat", "A little bird", "A squirrel", "A butterfly"], correct: 1, hint: "Look at the sentence about the tree.", explain: "The text says a little bird lives in the tree." },
      { q: "When does the grandmother water the plants?", options: ["Every evening", "Every morning", "Once a week", "On Sundays"], correct: 1, hint: "Look at the sentence about watering.", explain: "The text says she waters the plants every morning." },
      { q: "When do they eat vegetables from the garden?", options: ["In winter", "In summer", "In autumn", "All year"], correct: 1, hint: "Look at the sentence about eating vegetables.", explain: "The text says they eat vegetables in summer." },
      { q: "What color are the other flowers?", options: ["Yellow", "Blue", "Purple", "Orange"], correct: 0, hint: "Look at the second sentence.", explain: "The text says there are yellow flowers." },
      { q: "How does the writer feel about the garden?", options: ["Does not like it", "Loves it very much", "Is afraid of it", "Never visits it"], correct: 1, hint: "Look at the last sentence.", explain: "The text says the writer loves the garden very much." },
      { q: "Does the grandmother grow any vegetables?", options: ["No, only flowers", "Yes, tomatoes and carrots", "Only fruit", "The text does not say"], correct: 1, hint: "Look at the third sentence.", explain: "She grows tomatoes and carrots." },
    ],
  },
  {
    level: "A2",
    title: "A Trip to the Zoo",
    text:
      "Last Sunday, my family went to the zoo. We saw many animals. The lions were sleeping in the sun. My little sister loved the monkeys because they were funny. We watched the penguins swim in cold water. At lunchtime, we ate sandwiches near the elephants. Before we left, I bought a small toy giraffe. It was a wonderful day.",
    questions: [
      { q: "When did the family go to the zoo?", options: ["Last Saturday", "Last Sunday", "Last Monday", "Yesterday"], correct: 1, hint: "Look at the first sentence.", explain: "The text says they went last Sunday." },
      { q: "What were the lions doing?", options: ["Eating", "Sleeping in the sun", "Running", "Roaring"], correct: 1, hint: "Look at the sentence about lions.", explain: "The text says the lions were sleeping in the sun." },
      { q: "Which animals did the little sister love?", options: ["The lions", "The monkeys", "The penguins", "The elephants"], correct: 1, hint: "Look at the sentence about the sister.", explain: "The text says she loved the monkeys." },
      { q: "Why did the sister love those animals?", options: ["They were big", "They were funny", "They were fast", "They were loud"], correct: 1, hint: "Look at the sentence about the sister.", explain: "The text says she loved the monkeys because they were funny." },
      { q: "What were the penguins doing?", options: ["Sleeping", "Swimming in cold water", "Eating fish", "Walking on ice"], correct: 1, hint: "Look at the sentence about penguins.", explain: "The text says they watched the penguins swim in cold water." },
      { q: "Where did the family eat lunch?", options: ["Near the elephants", "In a restaurant", "At home", "Near the lions"], correct: 0, hint: "Look at the sentence about lunchtime.", explain: "The text says they ate sandwiches near the elephants." },
      { q: "What did they eat at lunchtime?", options: ["Pizza", "Sandwiches", "Ice cream", "Fruit"], correct: 1, hint: "Look at the sentence about lunchtime.", explain: "The text says they ate sandwiches." },
      { q: "What did the writer buy before leaving?", options: ["A toy lion", "A small toy giraffe", "A book", "A hat"], correct: 1, hint: "Look at the sentence before the last.", explain: "The text says the writer bought a small toy giraffe." },
      { q: "How did the writer describe the day?", options: ["Boring", "Wonderful", "Tiring", "Too short"], correct: 1, hint: "Look at the last sentence.", explain: "The text says it was a wonderful day." },
      { q: "Who went to the zoo?", options: ["The writer alone", "The writer's family", "A school class", "Some friends"], correct: 1, hint: "Look at the first sentence.", explain: "The text says the writer's family went to the zoo." },
    ],
  },
  {
    level: "A2",
    title: "Learning to Cook",
    text:
      "My brother is learning to cook. Last week, he made pasta for the whole family. First, he boiled water in a big pot. Then he added the pasta and some salt. While the pasta cooked, he prepared a tomato sauce. He was a little nervous, but the food tasted great. Now he wants to cook dinner every weekend.",
    questions: [
      { q: "Who is learning to cook?", options: ["The writer", "The writer's brother", "The writer's mother", "A friend"], correct: 1, hint: "Look at the first sentence.", explain: "The text says the writer's brother is learning to cook." },
      { q: "What did the brother make last week?", options: ["Pizza", "Pasta", "Soup", "Cake"], correct: 1, hint: "Look at the second sentence.", explain: "The text says he made pasta." },
      { q: "Who did he cook for?", options: ["Just himself", "The whole family", "His friends", "His teacher"], correct: 1, hint: "Look at the second sentence.", explain: "The text says he made pasta for the whole family." },
      { q: "What did he do first?", options: ["Added the pasta", "Boiled water in a big pot", "Made the sauce", "Set the table"], correct: 1, hint: "Look at the sentence starting with 'First'.", explain: "The text says first he boiled water in a big pot." },
      { q: "What did he add to the water?", options: ["Only pasta", "The pasta and some salt", "Sugar", "Oil"], correct: 1, hint: "Look at the sentence starting with 'Then'.", explain: "The text says he added the pasta and some salt." },
      { q: "What sauce did he prepare?", options: ["A cheese sauce", "A tomato sauce", "A cream sauce", "No sauce"], correct: 1, hint: "Look at the sentence about the sauce.", explain: "The text says he prepared a tomato sauce." },
      { q: "How did the brother feel while cooking?", options: ["Very calm", "A little nervous", "Angry", "Bored"], correct: 1, hint: "Look at the sentence about his feelings.", explain: "The text says he was a little nervous." },
      { q: "How did the food taste?", options: ["Great", "Terrible", "Too salty", "Cold"], correct: 0, hint: "Look at the sentence about the food.", explain: "The text says the food tasted great." },
      { q: "What does the brother want to do now?", options: ["Stop cooking", "Cook dinner every weekend", "Open a restaurant", "Cook only pasta"], correct: 1, hint: "Look at the last sentence.", explain: "The text says he wants to cook dinner every weekend." },
      { q: "When did the brother make the pasta?", options: ["Yesterday", "Last week", "Last month", "Today"], correct: 1, hint: "Look at the second sentence.", explain: "The text says he made pasta last week." },
    ],
  },
  {
    level: "B1",
    title: "The Benefits of Reading",
    text:
      "Reading regularly offers many benefits for people of all ages. It improves vocabulary and helps readers express their ideas more clearly. Studies suggest that reading fiction can increase empathy, because readers imagine the lives and feelings of different characters. Reading before bed can also help people relax and sleep better than looking at a screen. Although some people say they have no time to read, experts recommend starting with just ten minutes a day. Over time, this small habit can make a big difference.",
    questions: [
      { q: "Who can benefit from reading regularly, according to the text?", options: ["Only children", "People of all ages", "Only students", "Only writers"], correct: 1, hint: "Look at the first sentence.", explain: "The text says reading offers benefits for people of all ages." },
      { q: "What language skill does reading improve?", options: ["Handwriting", "Vocabulary", "Pronunciation only", "Spelling only"], correct: 1, hint: "Look at the second sentence.", explain: "The text says reading improves vocabulary." },
      { q: "What can reading fiction increase, according to studies?", options: ["Empathy", "Speed", "Wealth", "Height"], correct: 0, hint: "Look at the sentence about fiction.", explain: "The text says reading fiction can increase empathy." },
      { q: "Why does fiction increase empathy, according to the text?", options: ["It is difficult", "Readers imagine the lives and feelings of characters", "It is short", "It has pictures"], correct: 1, hint: "Look at the sentence about fiction.", explain: "The text says readers imagine the lives and feelings of different characters." },
      { q: "What can reading before bed help people do?", options: ["Relax and sleep better", "Stay awake longer", "Eat less", "Exercise more"], correct: 0, hint: "Look at the sentence about bed.", explain: "The text says reading before bed can help people relax and sleep better." },
      { q: "What is reading before bed compared to in the text?", options: ["Listening to music", "Looking at a screen", "Drinking tea", "Going for a walk"], correct: 1, hint: "Look at the sentence about bed.", explain: "The text compares it to looking at a screen." },
      { q: "What reason do some people give for not reading?", options: ["They dislike books", "They have no time", "They cannot read", "Books are expensive"], correct: 1, hint: "Look at the sentence starting with 'Although'.", explain: "The text says some people say they have no time to read." },
      { q: "How much reading do experts recommend starting with?", options: ["One hour a day", "Ten minutes a day", "Two books a week", "Thirty minutes a day"], correct: 1, hint: "Look at the sentence about experts.", explain: "The text says experts recommend starting with just ten minutes a day." },
      { q: "What can the small habit of reading do over time?", options: ["Make a big difference", "Waste time", "Cost a lot of money", "Become boring"], correct: 0, hint: "Look at the last sentence.", explain: "The text says over time this small habit can make a big difference." },
      { q: "According to the text, does reading help people express ideas?", options: ["No", "Yes, more clearly", "Only in writing", "Only for experts"], correct: 1, hint: "Look at the second sentence.", explain: "The text says it helps readers express their ideas more clearly." },
    ],
  },
  {
    level: "B1",
    title: "Volunteering in the Community",
    text:
      "Many people choose to spend some of their free time volunteering in their local community. Volunteers might help at a food bank, teach children to read, or care for animals at a shelter. Besides helping others, volunteering can also benefit the volunteers themselves. It allows them to learn new skills, meet new people, and feel more connected to their community. Some studies even suggest that people who volunteer regularly report feeling happier and less stressed. For many, the sense of purpose it brings is the greatest reward.",
    questions: [
      { q: "What do many people choose to do with some free time, according to the text?", options: ["Volunteer in their community", "Watch television", "Travel abroad", "Sleep more"], correct: 0, hint: "Look at the first sentence.", explain: "The text says many people volunteer in their local community." },
      { q: "Which is an example of volunteering given in the text?", options: ["Helping at a food bank", "Running a company", "Selling products", "Driving a taxi"], correct: 0, hint: "Look at the second sentence.", explain: "The text mentions helping at a food bank." },
      { q: "Besides helping others, who else can volunteering benefit?", options: ["No one", "The volunteers themselves", "Only companies", "Only children"], correct: 1, hint: "Look at the sentence about benefits.", explain: "The text says volunteering can also benefit the volunteers themselves." },
      { q: "What does volunteering allow people to learn?", options: ["New skills", "New languages only", "Nothing new", "Only cooking"], correct: 0, hint: "Look at the sentence about skills.", explain: "The text says it allows them to learn new skills." },
      { q: "How do regular volunteers report feeling, according to studies?", options: ["Happier and less stressed", "Tired and bored", "Angry", "Lonely"], correct: 0, hint: "Look at the sentence about studies.", explain: "The text says they report feeling happier and less stressed." },
      { q: "What might volunteers do at a shelter?", options: ["Care for animals", "Cook meals", "Build houses", "Teach math"], correct: 0, hint: "Look at the second sentence.", explain: "The text mentions caring for animals at a shelter." },
      { q: "What social benefit does volunteering bring, according to the text?", options: ["Meeting new people", "Earning money", "Getting famous", "Winning prizes"], correct: 0, hint: "Look at the sentence about benefits.", explain: "The text says volunteers meet new people." },
      { q: "For many volunteers, what is the greatest reward?", options: ["Money", "A sense of purpose", "Free food", "Fame"], correct: 1, hint: "Look at the last sentence.", explain: "The text says the sense of purpose is the greatest reward." },
      { q: "What might volunteers teach children to do?", options: ["To read", "To swim", "To drive", "To cook"], correct: 0, hint: "Look at the second sentence.", explain: "The text mentions teaching children to read." },
      { q: "Does volunteering help people feel connected to their community?", options: ["No", "Yes", "Only sometimes for money", "The text does not say"], correct: 1, hint: "Look at the sentence about connection.", explain: "The text says it helps them feel more connected to their community." },
    ],
  },
  {
    level: "B2",
    title: "The Importance of Sleep",
    text:
      "Sleep is often the first thing people sacrifice when life becomes busy, yet scientists increasingly warn that this is a serious mistake. During sleep, the brain consolidates memories, repairs cells, and clears out waste products that build up during the day. Chronic sleep deprivation has been linked to a range of problems, including weakened immunity, difficulty concentrating, and a higher risk of certain diseases. Despite this, many adults regularly get less than the recommended seven to nine hours. Experts suggest keeping a consistent sleep schedule and avoiding screens before bed as practical ways to improve sleep quality.",
    questions: [
      { q: "What do people often sacrifice when life becomes busy?", options: ["Food", "Sleep", "Exercise", "Work"], correct: 1, hint: "Look at the first sentence.", explain: "The text says sleep is often the first thing people sacrifice." },
      { q: "How do scientists view sacrificing sleep?", options: ["As a good idea", "As a serious mistake", "As unimportant", "As necessary"], correct: 1, hint: "Look at the first sentence.", explain: "The text says scientists warn it is a serious mistake." },
      { q: "What does the brain do during sleep, according to the text?", options: ["Nothing", "Consolidates memories and repairs cells", "Uses more energy", "Grows larger"], correct: 1, hint: "Look at the second sentence.", explain: "The text says the brain consolidates memories, repairs cells, and clears waste." },
      { q: "What has chronic sleep deprivation been linked to?", options: ["Better health", "Weakened immunity and poor concentration", "More energy", "Faster memory"], correct: 1, hint: "Look at the sentence about deprivation.", explain: "The text links it to weakened immunity, difficulty concentrating, and disease risk." },
      { q: "How many hours of sleep are recommended, according to the text?", options: ["Four to five", "Seven to nine", "Ten to twelve", "Three to four"], correct: 1, hint: "Look at the sentence about hours.", explain: "The text mentions the recommended seven to nine hours." },
      { q: "Do many adults get enough sleep, according to the text?", options: ["Yes, always", "No, many get less than recommended", "Only children do not", "The text does not say"], correct: 1, hint: "Look at the sentence starting with 'Despite this'.", explain: "The text says many adults regularly get less than the recommended amount." },
      { q: "What do experts suggest for better sleep?", options: ["Drinking coffee at night", "Keeping a consistent sleep schedule", "Sleeping in the day", "Exercising at midnight"], correct: 1, hint: "Look at the last sentence.", explain: "The text says experts suggest keeping a consistent sleep schedule." },
      { q: "What should people avoid before bed, according to experts?", options: ["Reading", "Screens", "Warm milk", "Quiet rooms"], correct: 1, hint: "Look at the last sentence.", explain: "The text says experts suggest avoiding screens before bed." },
      { q: "What builds up in the brain during the day?", options: ["Memories", "Waste products", "Water", "Energy"], correct: 1, hint: "Look at the second sentence.", explain: "The text says the brain clears out waste products that build up during the day." },
      { q: "What is the main message of the text?", options: ["Sleep is a waste of time", "Sleep is very important for health", "People sleep too much", "Sleep has no effect"], correct: 1, hint: "Consider the whole passage.", explain: "The text argues that sleep is important and should not be sacrificed." },
    ],
  },
  {
    level: "B2",
    title: "The Growth of Online Shopping",
    text:
      "Over the last two decades, online shopping has transformed the way people buy goods. Consumers can now compare prices, read reviews, and order products from around the world without leaving their homes. This convenience has come at a cost, however, as many traditional shops have struggled to compete and some have closed entirely. Online shopping has also raised concerns about the environmental impact of packaging and rapid delivery. Despite these challenges, most analysts expect online retail to keep growing, especially as technology makes the experience faster and more personalized.",
    questions: [
      { q: "What has online shopping transformed over the last two decades?", options: ["The way people travel", "The way people buy goods", "The way people cook", "The way people sleep"], correct: 1, hint: "Look at the first sentence.", explain: "The text says online shopping has transformed the way people buy goods." },
      { q: "What can consumers now do, according to the text?", options: ["Compare prices and read reviews", "Only buy local products", "Avoid all shops", "Pay only with cash"], correct: 0, hint: "Look at the second sentence.", explain: "The text says consumers can compare prices, read reviews, and order from around the world." },
      { q: "What negative effect has this convenience had on traditional shops?", options: ["They became more popular", "Many have struggled and some closed", "They lowered prices", "They moved online easily"], correct: 1, hint: "Look at the sentence starting with 'This convenience'.", explain: "The text says many traditional shops have struggled and some have closed." },
      { q: "What environmental concerns has online shopping raised?", options: ["Packaging and rapid delivery", "Water use", "Air travel", "Farming"], correct: 0, hint: "Look at the sentence about the environment.", explain: "The text mentions concerns about packaging and rapid delivery." },
      { q: "What do most analysts expect for online retail?", options: ["It will disappear", "It will keep growing", "It will stay the same", "It will become illegal"], correct: 1, hint: "Look at the last sentence.", explain: "The text says most analysts expect online retail to keep growing." },
      { q: "Why do analysts expect this trend, according to the text?", options: ["Because shops are closing", "Because technology makes it faster and more personalized", "Because it is cheaper for shops", "Because people dislike stores"], correct: 1, hint: "Look at the last sentence.", explain: "The text says technology makes the experience faster and more personalized." },
      { q: "Where can consumers order products from now?", options: ["Only their own city", "From around the world", "Only from one country", "Only from local shops"], correct: 1, hint: "Look at the second sentence.", explain: "The text says consumers can order products from around the world." },
      { q: "Do consumers need to leave home to shop online?", options: ["Yes", "No", "Only for large items", "Only on weekends"], correct: 1, hint: "Look at the second sentence.", explain: "The text says they can shop without leaving their homes." },
      { q: "How does the text describe the effect of online shopping overall?", options: ["Entirely positive", "Both convenient and challenging", "Entirely negative", "Unimportant"], correct: 1, hint: "Consider the whole passage.", explain: "The text presents both convenience and costs such as closed shops and environmental concerns." },
      { q: "How long has online shopping been transforming buying, according to the text?", options: ["Over the last two decades", "For one year", "For fifty years", "For a few months"], correct: 0, hint: "Look at the first sentence.", explain: "The text says over the last two decades." },
    ],
  },
  {
    level: "C1",
    title: "The Psychology of Habits",
    text:
      "Habits are the automatic behaviors that shape a surprising proportion of our daily lives, from the route we take to work to the way we respond to stress. Psychologists describe habits as following a loop consisting of a cue, a routine, and a reward. Once this loop is firmly established, the brain stops actively deciding and simply follows the pattern, which conserves mental energy but can also entrench undesirable behaviors. Research suggests that the most effective way to change a habit is not to eliminate it outright but to keep the same cue and reward while replacing the routine. Understanding this mechanism gives individuals a practical framework for building healthier habits and gradually dismantling harmful ones.",
    questions: [
      { q: "What are habits, according to the text?", options: ["Rare events", "Automatic behaviors that shape much of daily life", "Conscious decisions", "Difficult skills"], correct: 1, hint: "Look at the first sentence.", explain: "The text describes habits as automatic behaviors that shape a surprising proportion of daily life." },
      { q: "What three parts make up the habit loop?", options: ["A cue, a routine, and a reward", "A start, a middle, and an end", "A cause, an effect, and a result", "A plan, an action, and a review"], correct: 0, hint: "Look at the second sentence.", explain: "The text says the loop consists of a cue, a routine, and a reward." },
      { q: "What happens once the loop is firmly established?", options: ["The brain decides carefully each time", "The brain stops actively deciding and follows the pattern", "The habit disappears", "The reward changes"], correct: 1, hint: "Look at the third sentence.", explain: "The text says the brain stops actively deciding and simply follows the pattern." },
      { q: "What is one benefit of following an established loop?", options: ["It conserves mental energy", "It costs more energy", "It slows the brain", "It removes rewards"], correct: 0, hint: "Look at the third sentence.", explain: "The text says following the pattern conserves mental energy." },
      { q: "What is a downside of firmly established loops?", options: ["They use too much energy", "They can entrench undesirable behaviors", "They require constant thought", "They are easy to change"], correct: 1, hint: "Look at the third sentence.", explain: "The text says the loop can entrench undesirable behaviors." },
      { q: "What is the most effective way to change a habit, according to research?", options: ["Eliminate it outright", "Keep the same cue and reward while replacing the routine", "Change the reward only", "Ignore the cue"], correct: 1, hint: "Look at the sentence about research.", explain: "The text says to keep the same cue and reward while replacing the routine." },
      { q: "What does understanding this mechanism give individuals?", options: ["A practical framework for building healthier habits", "A cure for all diseases", "A way to avoid all habits", "More free time only"], correct: 0, hint: "Look at the last sentence.", explain: "The text says it gives a practical framework for building healthier habits." },
      { q: "Which everyday example of a habit does the text mention?", options: ["The route we take to work", "The food we cook", "The books we read", "The clothes we buy"], correct: 0, hint: "Look at the first sentence.", explain: "The text mentions the route we take to work." },
      { q: "According to the text, should you try to eliminate a habit outright?", options: ["Yes, always", "No, it is better to replace the routine", "Only for good habits", "The text does not say"], correct: 1, hint: "Look at the sentence about research.", explain: "The text says the most effective way is not to eliminate it but to replace the routine." },
      { q: "What does the habit loop allow the brain to avoid?", options: ["Active decision-making each time", "Sleeping", "Eating", "Learning"], correct: 0, hint: "Look at the third sentence.", explain: "The text says the brain stops actively deciding." },
    ],
  },
  {
    level: "C1",
    title: "The Challenges of Urbanization",
    text:
      "As more of the world's population moves from rural areas into cities, urban planners face mounting challenges in accommodating this rapid growth. Overcrowding can strain public services, drive up housing costs, and increase pollution, particularly in cities that expand faster than their infrastructure. At the same time, well-managed urbanization can bring significant benefits, concentrating economic opportunity, education, and healthcare in ways that improve living standards. The key, many experts argue, lies in thoughtful planning that prioritizes affordable housing, efficient public transport, and green spaces. Cities that fail to plan for growth often find the problems far harder and more expensive to solve later.",
    questions: [
      { q: "Where is the world's population increasingly moving, according to the text?", options: ["From cities to rural areas", "From rural areas into cities", "To other countries", "To the coast"], correct: 1, hint: "Look at the first sentence.", explain: "The text says people move from rural areas into cities." },
      { q: "Who faces mounting challenges from this growth?", options: ["Farmers", "Urban planners", "Tourists", "Teachers"], correct: 1, hint: "Look at the first sentence.", explain: "The text says urban planners face mounting challenges." },
      { q: "What can overcrowding do, according to the text?", options: ["Strain public services and raise housing costs", "Lower pollution", "Reduce housing costs", "Improve infrastructure"], correct: 0, hint: "Look at the second sentence.", explain: "The text says overcrowding can strain services, drive up housing costs, and increase pollution." },
      { q: "When is pollution especially a problem?", options: ["When cities expand faster than their infrastructure", "When cities are small", "When people leave cities", "When housing is cheap"], correct: 0, hint: "Look at the second sentence.", explain: "The text says particularly in cities that expand faster than their infrastructure." },
      { q: "What benefit can well-managed urbanization bring?", options: ["Concentrating economic opportunity and services", "More rural farming", "Fewer people", "Lower education levels"], correct: 0, hint: "Look at the third sentence.", explain: "The text says it can concentrate economic opportunity, education, and healthcare." },
      { q: "What do experts say the key to good urbanization is?", options: ["Thoughtful planning", "Stopping all growth", "Building only offices", "Removing green spaces"], correct: 0, hint: "Look at the fourth sentence.", explain: "The text says the key lies in thoughtful planning." },
      { q: "What should thoughtful planning prioritize, according to the text?", options: ["Affordable housing, public transport, and green spaces", "Only skyscrapers", "Only roads", "Only shopping centers"], correct: 0, hint: "Look at the fourth sentence.", explain: "The text lists affordable housing, efficient public transport, and green spaces." },
      { q: "What happens to cities that fail to plan for growth?", options: ["They solve problems easily", "They find problems harder and more expensive to solve later", "They stop growing", "They have no problems"], correct: 1, hint: "Look at the last sentence.", explain: "The text says such cities find the problems far harder and more expensive to solve later." },
      { q: "Does the text present urbanization as only negative?", options: ["Yes", "No, it can bring significant benefits too", "It says nothing about benefits", "Only for planners"], correct: 1, hint: "Look at the third sentence.", explain: "The text says well-managed urbanization can bring significant benefits." },
      { q: "What can concentrating healthcare and education improve?", options: ["Living standards", "Pollution", "Traffic only", "Farming"], correct: 0, hint: "Look at the third sentence.", explain: "The text says it improves living standards." },
    ],
  },
  {
    level: "C2",
    title: "The Problem of Free Will",
    text:
      "Few questions have preoccupied philosophers as persistently as whether human beings genuinely possess free will. Determinists argue that every event, including every human decision, is the inevitable consequence of prior causes governed by the laws of nature, leaving no room for genuine choice. Libertarians, by contrast, maintain that individuals can act as true originators of their actions, exercising a freedom that cannot be reduced to mere physical processes. A third position, compatibilism, attempts to reconcile the two by redefining freedom not as the absence of causation but as the capacity to act according to one's own motives without external coercion. Far from a purely academic dispute, the debate carries profound implications for how societies assign moral responsibility and administer justice.",
    questions: [
      { q: "What central question does the text address?", options: ["Whether humans possess free will", "Whether the universe is infinite", "Whether animals think", "Whether time is real"], correct: 0, hint: "Look at the first sentence.", explain: "The text asks whether human beings genuinely possess free will." },
      { q: "What do determinists argue?", options: ["Every event is the inevitable consequence of prior causes", "Humans are entirely free", "Nothing can be predicted", "Choices have no causes"], correct: 0, hint: "Look at the sentence about determinists.", explain: "The text says determinists argue every event is the inevitable consequence of prior causes." },
      { q: "According to determinists, is there room for genuine choice?", options: ["Yes, complete freedom", "No room for genuine choice", "Only for some people", "Only in emergencies"], correct: 1, hint: "Look at the sentence about determinists.", explain: "The text says determinism leaves no room for genuine choice." },
      { q: "What do libertarians maintain?", options: ["Individuals can be true originators of their actions", "All actions are predetermined", "Freedom is an illusion", "Only laws of nature matter"], correct: 0, hint: "Look at the sentence about libertarians.", explain: "The text says libertarians maintain individuals can act as true originators of their actions." },
      { q: "What is the third position called?", options: ["Determinism", "Compatibilism", "Libertarianism", "Skepticism"], correct: 1, hint: "Look at the sentence about the third position.", explain: "The text names the third position compatibilism." },
      { q: "How does compatibilism redefine freedom?", options: ["As the absence of all causation", "As the capacity to act according to one's own motives without coercion", "As random behavior", "As following the laws of nature exactly"], correct: 1, hint: "Look at the sentence about compatibilism.", explain: "The text says compatibilism defines freedom as acting on one's own motives without external coercion." },
      { q: "What does compatibilism try to do?", options: ["Reject both other views", "Reconcile determinism and libertarianism", "Prove free will is fake", "End the debate forever"], correct: 1, hint: "Look at the sentence about compatibilism.", explain: "The text says compatibilism attempts to reconcile the two views." },
      { q: "Why is the debate more than academic, according to the text?", options: ["It affects how societies assign moral responsibility and justice", "It has no practical effect", "It is only about language", "It concerns only scientists"], correct: 0, hint: "Look at the last sentence.", explain: "The text says the debate carries implications for moral responsibility and justice." },
      { q: "According to libertarians, can freedom be reduced to physical processes?", options: ["Yes", "No, it cannot be reduced to mere physical processes", "Only partly", "The text does not say"], correct: 1, hint: "Look at the sentence about libertarians.", explain: "The text says libertarians see a freedom that cannot be reduced to mere physical processes." },
      { q: "What governs prior causes, according to determinists?", options: ["The laws of nature", "Human wishes", "Random chance", "Government rules"], correct: 0, hint: "Look at the sentence about determinists.", explain: "The text says prior causes are governed by the laws of nature." },
    ],
  },
  {
    level: "C2",
    title: "The Paradoxes of Globalization",
    text:
      "Globalization, the increasing interconnection of economies and cultures across national borders, has generated both unprecedented prosperity and persistent controversy. Proponents credit it with lifting hundreds of millions out of poverty by integrating developing nations into global markets and accelerating the exchange of ideas and technology. Critics counter that its benefits have been distributed unevenly, enriching some regions while hollowing out industries and communities in others, and that it has eroded local cultures in favor of a homogenized global consumerism. Complicating matters further, the very interconnectedness that fuels growth also transmits crises rapidly, as a financial shock or pandemic in one region can cascade across the entire system. Navigating these tensions remains one of the defining challenges of the contemporary era.",
    questions: [
      { q: "How does the text define globalization?", options: ["The increasing interconnection of economies and cultures across borders", "The closing of national borders", "The decline of technology", "The end of trade"], correct: 0, hint: "Look at the first sentence.", explain: "The text defines globalization as the increasing interconnection of economies and cultures across national borders." },
      { q: "What do proponents credit globalization with?", options: ["Lifting hundreds of millions out of poverty", "Causing all wars", "Ending all trade", "Slowing technology"], correct: 0, hint: "Look at the sentence about proponents.", explain: "The text says proponents credit it with lifting hundreds of millions out of poverty." },
      { q: "How do proponents say poverty was reduced?", options: ["By integrating developing nations into global markets", "By closing markets", "By stopping technology", "By raising borders"], correct: 0, hint: "Look at the sentence about proponents.", explain: "The text says it integrated developing nations into global markets." },
      { q: "What do critics say about the benefits of globalization?", options: ["They are distributed unevenly", "They are shared equally", "They do not exist", "They only help the poor"], correct: 0, hint: "Look at the sentence about critics.", explain: "The text says critics argue the benefits have been distributed unevenly." },
      { q: "What cultural concern do critics raise?", options: ["It has eroded local cultures in favor of global consumerism", "It has strengthened all local cultures", "It has ended all culture", "It has no cultural effect"], correct: 0, hint: "Look at the sentence about critics.", explain: "The text says critics argue it has eroded local cultures in favor of homogenized global consumerism." },
      { q: "What negative effect does interconnectedness have, according to the text?", options: ["It transmits crises rapidly", "It stops all growth", "It isolates regions", "It slows communication"], correct: 0, hint: "Look at the sentence about interconnectedness.", explain: "The text says interconnectedness transmits crises rapidly." },
      { q: "What example of a fast-spreading crisis does the text give?", options: ["A financial shock or pandemic", "A local festival", "A new invention", "A sports event"], correct: 0, hint: "Look at the sentence about crises.", explain: "The text mentions a financial shock or pandemic in one region cascading across the system." },
      { q: "How does the text describe navigating these tensions?", options: ["One of the defining challenges of the contemporary era", "A simple task", "An impossible goal", "An unimportant issue"], correct: 0, hint: "Look at the last sentence.", explain: "The text says navigating these tensions remains a defining challenge of the era." },
      { q: "Does the text present globalization as entirely good or bad?", options: ["Entirely good", "Entirely bad", "Both beneficial and controversial", "Neither"], correct: 2, hint: "Look at the first sentence.", explain: "The text says globalization has generated both prosperity and controversy." },
      { q: "What did globalization accelerate, according to proponents?", options: ["The exchange of ideas and technology", "The closing of borders", "The decline of trade", "The loss of jobs everywhere"], correct: 0, hint: "Look at the sentence about proponents.", explain: "The text says it accelerated the exchange of ideas and technology." },
    ],
  },
  {
    level: "C1",
    title: "The Cognitive Benefits of Bilingualism",
    text:
      "For much of the twentieth century, conventional wisdom held that raising a child with two languages would inevitably cause confusion and impede intellectual development. Contemporary research has comprehensively overturned this assumption. Far from hindering cognition, bilingualism appears to confer a range of subtle yet significant advantages. Because bilingual individuals must continually select the appropriate language while suppressing the other, they exercise the brain's executive control system, the network responsible for attention, planning, and switching between tasks, far more intensively than monolinguals. This constant mental juggling seems to translate into enhanced cognitive flexibility and a heightened ability to filter out irrelevant information. Perhaps the most striking findings concern aging: several studies suggest that lifelong bilingualism can delay the onset of dementia symptoms by several years, presumably by building what researchers call 'cognitive reserve'. It would be misleading, however, to portray bilingualism as a panacea. The effects are often modest, and methodological disputes persist over how consistently they can be replicated. What is increasingly clear is that the bilingual brain is not a confused brain but an adaptable one, continually reshaped by the demands placed upon it.",
    questions: [
      { q: "What did conventional wisdom in the twentieth century claim about raising a child with two languages?", options: ["It would enhance intelligence", "It would inevitably cause confusion and impede development", "It had no effect at all", "It was impossible"], correct: 1, hint: "Look at the first sentence.", explain: "The text says conventional wisdom held it would inevitably cause confusion and impede intellectual development." },
      { q: "How does contemporary research relate to that earlier assumption?", options: ["It confirms it", "It has comprehensively overturned it", "It ignores it", "It only partly supports it"], correct: 1, hint: "Look at the second sentence.", explain: "The text says contemporary research has comprehensively overturned the assumption." },
      { q: "Why do bilingual individuals exercise the executive control system more intensively?", options: ["Because they read more books", "Because they must continually select one language while suppressing the other", "Because they sleep less", "Because they travel more"], correct: 1, hint: "Look at the sentence about executive control.", explain: "The text says they must continually select the appropriate language while suppressing the other." },
      { q: "What is the executive control system responsible for, according to the text?", options: ["Attention, planning, and switching between tasks", "Digestion and breathing", "Only long-term memory", "Physical coordination"], correct: 0, hint: "Look at the description of the network.", explain: "The text describes it as the network responsible for attention, planning, and switching between tasks." },
      { q: "What does this constant mental juggling seem to produce?", options: ["Enhanced cognitive flexibility and better filtering of irrelevant information", "Chronic tiredness", "Slower reactions", "A reduced vocabulary"], correct: 0, hint: "Look at the sentence after the executive control description.", explain: "The text says it translates into enhanced cognitive flexibility and a heightened ability to filter out irrelevant information." },
      { q: "What striking finding does the text report about aging?", options: ["Bilingualism causes earlier dementia", "Lifelong bilingualism can delay the onset of dementia symptoms by several years", "Bilingualism has no link to aging", "Bilingual people age faster"], correct: 1, hint: "Look at the sentence about aging.", explain: "The text says lifelong bilingualism can delay the onset of dementia symptoms by several years." },
      { q: "What do researchers call the protective factor built by bilingualism?", options: ["Cognitive reserve", "Mental fatigue", "Language overload", "Neural decay"], correct: 0, hint: "Look at the quoted term.", explain: "The text says it builds what researchers call 'cognitive reserve'." },
      { q: "Why would it be misleading to portray bilingualism as a panacea?", options: ["Because the effects are often modest and disputes persist over replication", "Because it has no benefits at all", "Because only children can benefit", "Because it is actually harmful"], correct: 0, hint: "Look at the sentence beginning 'It would be misleading'.", explain: "The text says the effects are often modest and methodological disputes persist over how consistently they can be replicated." },
      { q: "What persists among researchers regarding the findings?", options: ["Complete agreement", "Methodological disputes over replication", "Total indifference", "A ban on further study"], correct: 1, hint: "Look at the sentence about disputes.", explain: "The text says methodological disputes persist over how consistently the effects can be replicated." },
      { q: "What is the author's overall conclusion about the bilingual brain?", options: ["It is a confused brain", "It is an adaptable brain, continually reshaped by the demands placed on it", "It is identical to a monolingual brain", "It is a damaged brain"], correct: 1, hint: "Look at the last sentence.", explain: "The text concludes that the bilingual brain is not a confused brain but an adaptable one, continually reshaped by the demands placed upon it." },
    ],
  },
  {
    level: "C1",
    title: "The Rise of the Gig Economy",
    text:
      "Over the past decade, the so-called gig economy has expanded from a marginal phenomenon into a defining feature of the modern labor market. Powered by digital platforms that match workers with short-term tasks, it encompasses everything from ride-hailing and food delivery to freelance graphic design and software development. Advocates celebrate the flexibility it affords: workers can, in principle, choose when and how much they work, escaping the rigidities of the traditional nine-to-five. For some, particularly those juggling caring responsibilities or supplementing another income, this autonomy is genuinely liberating. Yet this rosy picture obscures a more troubling reality. Because gig workers are typically classified as independent contractors rather than employees, they are frequently denied the protections that previous generations fought hard to secure, such as paid leave, sick pay, pension contributions, and a guaranteed minimum wage. Critics contend that the vaunted flexibility is, for many, an illusion masking precariousness and unpredictable earnings. Governments across the world are now grappling with how to regulate this sector without stifling the innovation and convenience it has undeniably delivered. The outcome of these debates will shape not only the fortunes of millions of workers but also broader assumptions about what constitutes fair employment in the twenty-first century.",
    questions: [
      { q: "How has the gig economy changed over the past decade?", options: ["It has disappeared", "It has expanded from a marginal phenomenon into a defining feature of the labor market", "It has remained marginal", "It has been banned"], correct: 1, hint: "Look at the first sentence.", explain: "The text says it has expanded from a marginal phenomenon into a defining feature of the modern labor market." },
      { q: "What powers the gig economy, according to the text?", options: ["Government subsidies", "Digital platforms that match workers with short-term tasks", "Traditional factories", "University training programs"], correct: 1, hint: "Look at the second sentence.", explain: "The text says it is powered by digital platforms that match workers with short-term tasks." },
      { q: "What do advocates celebrate about the gig economy?", options: ["Its high wages", "The flexibility it affords", "Its job security", "Its pension benefits"], correct: 1, hint: "Look at the sentence about advocates.", explain: "The text says advocates celebrate the flexibility it affords." },
      { q: "For whom is the autonomy described as genuinely liberating?", options: ["Only company executives", "Those juggling caring responsibilities or supplementing another income", "Only full-time employees", "Only software developers"], correct: 1, hint: "Look at the sentence about autonomy.", explain: "The text mentions those juggling caring responsibilities or supplementing another income." },
      { q: "How are gig workers typically classified?", options: ["As employees", "As independent contractors rather than employees", "As volunteers", "As company owners"], correct: 1, hint: "Look at the sentence about classification.", explain: "The text says they are typically classified as independent contractors rather than employees." },
      { q: "Which protections are gig workers frequently denied?", options: ["Paid leave, sick pay, pension contributions, and a guaranteed minimum wage", "Access to the internet", "The right to vote", "Free transportation"], correct: 0, hint: "Look at the list in that sentence.", explain: "The text lists paid leave, sick pay, pension contributions, and a guaranteed minimum wage." },
      { q: "What do critics say about the much-praised flexibility?", options: ["It is genuine for everyone", "It is, for many, an illusion masking precariousness", "It does not exist at all", "It only benefits governments"], correct: 1, hint: "Look at the sentence about critics.", explain: "The text says critics contend the flexibility is, for many, an illusion masking precariousness and unpredictable earnings." },
      { q: "What challenge are governments now grappling with?", options: ["How to ban the gig economy", "How to regulate the sector without stifling innovation and convenience", "How to make everyone a gig worker", "How to eliminate digital platforms"], correct: 1, hint: "Look at the sentence about governments.", explain: "The text says governments are grappling with how to regulate the sector without stifling the innovation and convenience it has delivered." },
      { q: "What has the gig economy undeniably delivered, according to the text?", options: ["Innovation and convenience", "Higher taxes", "Guaranteed pensions", "Shorter hours for everyone"], correct: 0, hint: "Look at the sentence about regulation.", explain: "The text refers to the innovation and convenience the gig economy has undeniably delivered." },
      { q: "According to the final sentence, what will the outcome of these debates shape?", options: ["Only the profits of platforms", "The fortunes of millions of workers and assumptions about fair employment", "Nothing significant", "Only the price of food delivery"], correct: 1, hint: "Look at the last sentence.", explain: "The text says it will shape the fortunes of millions of workers and broader assumptions about what constitutes fair employment in the twenty-first century." },
    ],
  },
  {
    level: "C1",
    title: "The Preservation of Endangered Languages",
    text:
      "Of the roughly seven thousand languages spoken today, linguists estimate that nearly half may fall silent by the end of the century. A language is generally considered endangered when it is no longer being transmitted to children, who instead adopt a more dominant tongue offering greater economic and social opportunity. The forces driving this decline are formidable: globalization, urban migration, and educational systems that privilege national or international languages over local ones. To the casual observer, the disappearance of an obscure language spoken by a few hundred people might seem an inevitable, even trivial, consequence of progress. Linguists and anthropologists, however, regard each loss as a profound impoverishment. Every language embodies a unique way of categorizing experience, encoding knowledge about local ecosystems, medicinal plants, and oral histories that may exist nowhere else. When a language dies, this accumulated wisdom frequently vanishes with it. Efforts to reverse the trend have met with varying success: some communities have revitalized their languages through immersion schools and digital archives, while others have found the momentum of decline difficult to arrest. Ultimately, whether a language survives often depends less on the number of its speakers than on whether the community believes it is worth preserving.",
    questions: [
      { q: "How many languages do linguists estimate may fall silent by the end of the century?", options: ["Almost none", "Nearly half of the roughly seven thousand spoken today", "All of them", "Exactly one thousand"], correct: 1, hint: "Look at the first sentence.", explain: "The text says that of the roughly seven thousand languages spoken today, nearly half may fall silent by the end of the century." },
      { q: "When is a language generally considered endangered?", options: ["When it has fewer than a million speakers", "When it is no longer being transmitted to children", "When it lacks a writing system", "When it is spoken only in cities"], correct: 1, hint: "Look at the second sentence.", explain: "The text says a language is considered endangered when it is no longer being transmitted to children." },
      { q: "Why do children adopt a more dominant tongue?", options: ["Because it offers greater economic and social opportunity", "Because it is easier to write", "Because teachers force them to", "Because it sounds nicer"], correct: 0, hint: "Look at the second sentence.", explain: "The text says children adopt a dominant tongue offering greater economic and social opportunity." },
      { q: "Which forces are described as driving the decline?", options: ["Globalization, urban migration, and education systems that favor dominant languages", "War and famine only", "Climate change alone", "A lack of dictionaries"], correct: 0, hint: "Look at the sentence listing the forces.", explain: "The text lists globalization, urban migration, and educational systems that privilege national or international languages over local ones." },
      { q: "How might a casual observer view the disappearance of an obscure language?", options: ["As a tragedy", "As an inevitable, even trivial, consequence of progress", "As impossible", "As a crime"], correct: 1, hint: "Look at the sentence about the casual observer.", explain: "The text says to a casual observer it might seem an inevitable, even trivial, consequence of progress." },
      { q: "How do linguists and anthropologists regard each language loss?", options: ["As a profound impoverishment", "As a minor inconvenience", "As beneficial", "As irrelevant"], correct: 0, hint: "Look at the sentence contrasting with the casual observer.", explain: "The text says linguists and anthropologists regard each loss as a profound impoverishment." },
      { q: "What does every language embody, according to the text?", options: ["A unique way of categorizing experience and encoding local knowledge", "Only grammar rules", "The same information as every other language", "Nothing of real value"], correct: 0, hint: "Look at the sentence about what a language embodies.", explain: "The text says every language embodies a unique way of categorizing experience, encoding knowledge about local ecosystems, medicinal plants, and oral histories." },
      { q: "What often happens to accumulated wisdom when a language dies?", options: ["It is automatically preserved", "It frequently vanishes with the language", "It transfers to a dominant language", "It becomes more valuable"], correct: 1, hint: "Look at the sentence about a language dying.", explain: "The text says that when a language dies, this accumulated wisdom frequently vanishes with it." },
      { q: "How have efforts to reverse the trend fared?", options: ["They have all failed", "They have met with varying success", "They have all succeeded", "They have never been attempted"], correct: 1, hint: "Look at the sentence about efforts.", explain: "The text says efforts to reverse the trend have met with varying success." },
      { q: "According to the final sentence, what does a language's survival often depend on most?", options: ["The number of its speakers", "Whether the community believes it is worth preserving", "Government funding alone", "The existence of a writing system"], correct: 1, hint: "Look at the last sentence.", explain: "The text says survival often depends less on the number of speakers than on whether the community believes it is worth preserving." },
    ],
  },
];
