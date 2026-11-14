## An audience's language

An audience line may name a language: `Audience B: Tokyo, aged 25 to 29, in Japanese`. **Without one, the piece is in
English.** The languages offered are those whose script the engine draws, a separate model reads back, and a voice
speaks: German, French, Spanish, Portuguese, Japanese, Chinese (Simplified), Korean and Hindi (measured 2026-10-27),
and Arabic (2026-10-29), which has rules of its own — see its point below.

- **Every word that audience reads is in its language**: the title, every claim and label, the call to action, the
  end card, and every figure, written as that language writes it — `Locale.number(value, { locale: 'ja-JP' })` with the
  language's locale for the place (`ja-JP`, `de-DE`, `zh-CN`, `ko-KR`). Names stay as given: the product's, the
  company's, a person's. A quotation from a document is translated and attributed as a translation (*"from the
  filing, translated"*).
- **The facts are the same in every language; only the telling changes.** Write each line in English first — that
  is the source a reader of `direction.md` can check — then in the audience's language. Never drop, soften or add a
  claim in translation: a film that says more or less in Tokyo than in London makes a different claim to each.
- **Every line is translated back before it is used.** Write the audience's lines, numbered and nothing else, into
  `lines_<letter>.md` with your file tools, and have a separate model put them back into English without seeing your
  English:

  ```javascript
  const back = await Documents.ask('lines_b.md',
      'Translate each numbered line into English, literally, keeping its number. Add nothing and explain nothing.',
      { schema: { type: 'object', required: ['lines'], properties: { lines: { type: 'array', items: { type: 'object',
          required: ['n', 'english'], properties: { n: { type: 'integer' }, english: { type: 'string' } } } } } } });
  for (const line of back.answer.lines) log(line.n + ': ' + line.english);   // `answer` is the parsed reply
  ```

  Compare each back-translation with your English for the same claim, the same figure and the same scope ("all",
  "only", "per year"). **A line whose meaning moved is rewritten and asked again**, never waved through. Put the
  table — English, the line as set, its back-translation — in `direction.md`, and record it:
  `Stage.check('every line in <language> says what its English says', …, 'N lines, M rewritten')`.
- **The type follows the script.** Choose a face that has it from `Skia.Font.families()` — on Windows Yu Gothic
  (Japanese), Microsoft YaHei (Chinese), Malgun Gothic (Korean), Nirmala UI (Hindi); Noto Sans CJK and Noto Sans
  Devanagari where they are installed — and keep the
  film's Latin face for names and figures only if it sits well beside it. The manuals' advice on capitals, small
  caps, italics and tracking is for Latin type: Chinese, Japanese and Korean have no capitals and no true italics, so
  never fake either, and break a line only where the language allows (not inside a word, and in Japanese not before
  a small kana or closing bracket).
- **In a film, reading time is counted in what the line says, not in its words.** Chinese and Japanese put no spaces between
  words, so "three words a second" counts a whole sentence as one word. Hold each line at least as long as its
  English source would need: it says the same thing, and is read no faster.
- **Arabic: drawn, voiced and read back — by Gemini.** `Video.reader` says which reader this studio has. Gemini, the
  default, read correctly drawn Arabic exactly and wrongly drawn Arabic as drawn, a reversed line included; but it once
  read two transposed letters as the word they should have been, as a reader of any script may. Nemotron misread whole
  Arabic words that were drawn correctly, so with it reading, an audience line in Arabic says so (*"its text checked by
  eye, not read back"*). Either way:
  - **Right to left.** Set an Arabic line from the right of its column and mirror a two-column scene — the words on
    the right, the picture on the left. `comp.text`'s `origin` is the text's centre: read its box (`boundsAt`) and
    place its right edge. Numbers, Latin names and tickers stay left to right inside the line; the engine does that.
  - **Figures** come from `Locale.number` with the place's locale, which follows CLDR in choosing Western or
    Arabic-Indic digits; use one style for the whole film.
  - **Look at every Arabic line in its frame yourself**, letter by letter against what you set, whatever the read-back
    says: a line read as set can still carry a transposed pair the reader corrected. Record it:
    `Stage.check('every Arabic line was looked at in its frame', …, 'N lines, M faults')`. With the translation back,
    each Arabic line is checked for its meaning and for its letters.
  - **With Nemotron reading**, the read-back marks every Arabic line `unchecked`, and each Arabic reading is a stray of
    kind `'unreadable script'` (Hindi too). Answer `Verify.stray(n, 'misread')` only when the frame shows the line as
    set — `'real'` when it does not.
  - **Say so where the client reads**: `direction.md` and `questions.md` state how the Arabic was checked — read back,
    or with Nemotron by translation back and by eye — and that no one has listened to the voice's Arabic.
- **In a film, a voice speaks the audience's language too, timed by its recording whole.** Record each line in that
  language with `Assets.speak`, then time it with `Motion.speech(spoken.text, { audio: spoken.path, language: 'ja' })`
  — the language code is required: `Motion.speech`'s word-by-word timing is English, and without the code a Japanese
  line stops the script. The line comes back as one span, from where the voice starts to where it stops; time the
  scene to that, and keep every claim the voice makes on screen as well, since there is no per-word timing to check the
  two against. The read-back checks what the screen says, not what the voice says.
