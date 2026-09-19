namespace Yap.Client.Services;

/// <summary>One entry in the picker. <paramref name="Terms"/> is the search index: every word
/// of the name plus any extra spellings, lowercased and space-delimited with a leading space
/// so a <c>Contains(" " + term)</c> is a prefix-of-any-word match without splitting per query.</summary>
public readonly record struct Emoji(string Glyph, string Name, string Terms);

/// <summary>
/// A curated emoji set with search words, grouped the way a phone keyboard groups them.
/// Deliberately not the full Unicode table: the whole catalog below is ~14 KB of source that
/// parses once on first open, where an emoji-data dump with names and keywords is 400 KB+ and
/// would give back a large part of the payload the size pass just won. Anything missing can
/// still be typed or pasted; this is the set people reach for.
/// </summary>
public static class EmojiCatalog
{
    /// <summary>The group the picker fills from this device's own history, not from the table.</summary>
    public const string Recent = "Recent";

    // "<glyph> <name>[;<extra search words>]", entries separated by '|'. The name doubles as the
    // accessible label, so extra spellings people search for live after the ';' instead of in it.
    private static readonly (string Name, string Tab, string Packed)[] Groups =
    [
        ("Smileys", "😀", "😀 grinning face;smile happy|😃 grinning face with big eyes;happy|😄 grinning face with smiling eyes;laugh|😁 beaming face;grin|😆 grinning squinting face;lol laugh|😅 grinning face with sweat;relief nervous|🤣 rolling on the floor laughing;rofl lol|😂 face with tears of joy;lol crying laughing|🙂 slightly smiling face|🙃 upside down face;silly irony|😉 winking face;wink|😊 smiling face with smiling eyes;blush happy|😇 smiling face with halo;innocent angel|🥰 smiling face with hearts;love adore|😍 smiling face with heart eyes;love|🤩 star struck;excited amazed|😘 face blowing a kiss|😗 kissing face|😚 kissing face with closed eyes|😙 kissing face with smiling eyes|😋 face savoring food;yum tongue delicious|😛 face with tongue|😜 winking face with tongue;silly|🤪 zany face;goofy crazy|😝 squinting face with tongue|🤑 money mouth face;rich greedy|🤗 hugging face;hug|🤭 face with hand over mouth;oops giggle|🤫 shushing face;quiet secret hush|🤔 thinking face;hmm wondering|🤐 zipper mouth face;silence|🤨 face with raised eyebrow;suspicious skeptical|😐 neutral face;meh|😑 expressionless face|😶 face without mouth;speechless silent|😏 smirking face;smug|😒 unamused face;annoyed|🙄 face with rolling eyes;eyeroll|😬 grimacing face;awkward yikes|🤥 lying face;liar pinocchio|😌 relieved face;calm content|😔 pensive face;sad down|😪 sleepy face;tired|🤤 drooling face|😴 sleeping face;zzz asleep|😷 face with medical mask;sick ill|🤒 face with thermometer;fever sick|🤕 face with head bandage;hurt injured|🤢 nauseated face;sick gross|🤮 face vomiting;puke sick|🤧 sneezing face;achoo|🥵 hot face;heat sweating|🥶 cold face;freezing|🥴 woozy face;drunk dizzy|😵 face with crossed out eyes;dizzy knocked out|🤯 exploding head;mind blown shocked|🤠 cowboy hat face|🥳 partying face;celebrate party|😎 smiling face with sunglasses;cool|🤓 nerd face;glasses|🧐 face with monocle;inspect|😕 confused face|😟 worried face|🙁 slightly frowning face|😮 face with open mouth;wow surprised|😯 hushed face;surprised|😲 astonished face;shocked gasp|😳 flushed face;embarrassed blush|🥺 pleading face;puppy eyes begging|😦 frowning face with open mouth|😨 fearful face;scared|😰 anxious face with sweat;worried|😥 sad but relieved face|😢 crying face;cry tear sad|😭 loudly crying face;sob bawling|😱 face screaming in fear;shock|😖 confounded face|😣 persevering face;struggling|😞 disappointed face|😓 downcast face with sweat|😩 weary face;tired fed up|😫 tired face;exhausted|🥱 yawning face;bored sleepy|😤 face with steam from nose;triumph frustrated|😡 pouting face;angry rage mad|😠 angry face;mad|🤬 face with symbols on mouth;cursing swearing|😈 smiling face with horns;devil mischief|👿 angry face with horns;imp|💀 skull;dead|☠️ skull and crossbones;danger poison|💩 pile of poo;crap|🤡 clown face|👻 ghost;boo halloween|👽 alien;ufo|🤖 robot;bot ai|😺 grinning cat|😹 cat with tears of joy|😻 smiling cat with heart eyes|🙀 weary cat;shocked|😿 crying cat"),
        ("People", "👋", "👋 waving hand;hello bye hi wave|🤚 raised back of hand|✋ raised hand;stop high five|🖖 vulcan salute;spock|👌 ok hand;perfect|🤌 pinched fingers;italian|🤏 pinching hand;small tiny|✌️ victory hand;peace|🤞 crossed fingers;luck hope|🤟 love you gesture|🤘 sign of the horns;rock|🤙 call me hand;shaka|👈 backhand index pointing left|👉 backhand index pointing right|👆 backhand index pointing up|👇 backhand index pointing down;this|☝️ index pointing up|👍 thumbs up;like yes approve good|👎 thumbs down;dislike no bad|✊ raised fist;power|👊 oncoming fist;punch fist bump|🤛 left facing fist|🤜 right facing fist|👏 clapping hands;applause bravo|🙌 raising hands;celebrate praise|👐 open hands|🤲 palms up together|🤝 handshake;deal agreement|🙏 folded hands;please thanks pray|✍️ writing hand|💅 nail polish;manicure|💪 flexed biceps;strong muscle gym|🦾 mechanical arm|🧠 brain;smart|👀 eyes;look watching|👁️ eye|👄 mouth;lips|💋 kiss mark;lipstick|🧑 person|👶 baby|🧒 child|👦 boy|👧 girl|👨 man|👩 woman|🧓 older person|👴 old man|👵 old woman|🙋 person raising hand;question volunteer|🙆 person gesturing ok|🙅 person gesturing no;stop refuse|🤷 person shrugging;shrug dunno|🤦 person facepalming;facepalm|💁 person tipping hand;sassy information|🕺 man dancing|💃 woman dancing;dance|🧑‍💻 technologist;developer coding|👮 police officer;cop|🧑‍🍳 cook;chef|🧑‍🚀 astronaut|🦸 superhero;hero|🧙 mage;wizard|🧚 fairy|🎅 santa claus;christmas"),
        ("Nature", "🐶", "🐶 dog face;puppy|🐱 cat face;kitten|🐭 mouse face|🐹 hamster|🐰 rabbit face;bunny|🦊 fox|🐻 bear|🐼 panda|🐨 koala|🐯 tiger face|🦁 lion|🐮 cow face|🐷 pig face|🐸 frog|🐵 monkey face|🙈 see no evil monkey|🙉 hear no evil monkey|🙊 speak no evil monkey|🐔 chicken|🐧 penguin|🐦 bird|🐤 baby chick|🦆 duck|🦅 eagle|🦉 owl|🐺 wolf|🐴 horse face|🦄 unicorn|🐝 honeybee;bee|🐛 bug;caterpillar|🦋 butterfly|🐌 snail|🐞 lady beetle;ladybug|🐜 ant|🕷️ spider|🐢 turtle|🐍 snake|🦎 lizard|🐙 octopus|🦀 crab|🐠 tropical fish|🐟 fish|🐬 dolphin|🐳 whale|🦈 shark|🐘 elephant|🦒 giraffe|🌵 cactus|🎄 christmas tree|🌲 evergreen tree|🌳 deciduous tree|🌴 palm tree|🌱 seedling;sprout growth|🌿 herb;plant|☘️ shamrock|🍀 four leaf clover;luck|🍁 maple leaf|🍂 fallen leaves;autumn fall|🍃 leaf fluttering in wind|🌷 tulip|🌹 rose|🌺 hibiscus|🌸 cherry blossom;sakura|🌼 blossom|🌻 sunflower|🌙 crescent moon;night|⭐ star|🌟 glowing star|✨ sparkles;shiny magic|⚡ high voltage;lightning|🔥 fire;lit hot flame|🌈 rainbow|☀️ sun;sunny|⛅ sun behind cloud|☁️ cloud|🌧️ cloud with rain|⛈️ cloud with lightning and rain;storm|❄️ snowflake;cold snow|⛄ snowman|💧 droplet;water|🌊 water wave;ocean sea"),
        ("Food", "🍎", "🍏 green apple|🍎 red apple;apple|🍐 pear|🍊 tangerine;orange|🍋 lemon|🍌 banana|🍉 watermelon|🍇 grapes|🍓 strawberry|🫐 blueberries|🍒 cherries|🍑 peach|🥭 mango|🍍 pineapple|🥥 coconut|🥝 kiwi fruit|🍅 tomato|🥑 avocado|🍆 eggplant;aubergine|🥕 carrot|🌽 corn|🌶️ hot pepper;spicy chilli|🥦 broccoli|🧄 garlic|🧅 onion|🍄 mushroom|🥐 croissant|🍞 bread|🥖 baguette|🧀 cheese|🥚 egg|🍳 cooking;fried egg breakfast|🥞 pancakes|🧇 waffle|🥓 bacon|🍔 hamburger;burger|🍟 french fries;chips|🍕 pizza|🌭 hot dog|🥪 sandwich|🌮 taco|🌯 burrito|🥗 green salad|🍝 spaghetti;pasta|🍜 steaming bowl;ramen noodles|🍣 sushi|🍤 fried shrimp|🍚 cooked rice|🍦 soft ice cream|🍩 doughnut;donut|🍪 cookie;biscuit|🎂 birthday cake|🍰 shortcake;cake slice|🧁 cupcake|🍫 chocolate bar|🍬 candy;sweet|🍭 lollipop|🍯 honey pot|🍿 popcorn|☕ hot beverage;coffee tea|🍵 teacup without handle;green tea|🧋 bubble tea;boba|🥤 cup with straw;soda|🍺 beer mug|🍻 clinking beer mugs;cheers|🥂 clinking glasses;champagne cheers celebrate|🍷 wine glass|🥃 tumbler glass;whisky|🍸 cocktail glass|🧊 ice"),
        ("Activity", "⚽", "⚽ soccer ball;football|🏀 basketball|🏈 american football|⚾ baseball|🎾 tennis|🏐 volleyball|🏉 rugby football|🎱 pool 8 ball;billiards|🏓 ping pong;table tennis|🏸 badminton|🏒 ice hockey|⛳ flag in hole;golf|🏹 bow and arrow;archery|🎣 fishing pole|🥊 boxing glove|🥋 martial arts uniform;karate judo|🛹 skateboard|⛸️ ice skate|🎿 skis|🏂 snowboarder|🏋️ person lifting weights;gym workout|🤸 person cartwheeling|🏊 person swimming|🚴 person biking;cycling|🏆 trophy;win champion|🥇 first place medal;gold win|🥈 second place medal;silver|🥉 third place medal;bronze|🎯 bullseye;dart target|🎮 video game;controller gaming|🕹️ joystick|🎲 game die;dice|🎭 performing arts;theatre drama|🎨 artist palette;art paint|🎤 microphone;sing karaoke|🎧 headphone;music listening|🎸 guitar|🎹 musical keyboard;piano|🥁 drum|🎺 trumpet|🎻 violin|🎬 clapper board;movie film|🎉 party popper;celebrate hooray congrats|🎊 confetti ball|🎈 balloon|🎁 wrapped gift;present birthday|🎀 ribbon"),
        ("Travel", "🚗", "🚗 car;automobile|🚕 taxi|🚙 sport utility vehicle;suv|🚌 bus|🏎️ racing car|🚓 police car|🚑 ambulance|🚒 fire engine|🚚 delivery truck|🚜 tractor|🛵 motor scooter|🏍️ motorcycle|🚲 bicycle;bike|🛴 kick scooter|🚄 bullet train|🚂 locomotive;train steam|🚇 metro;subway underground|🚊 tram|🚁 helicopter|✈️ airplane;plane flight|🛫 airplane departure;takeoff|🛬 airplane arrival;landing|🚀 rocket;launch space|🛸 flying saucer;ufo|⛵ sailboat|🚤 speedboat|🛳️ passenger ship;cruise|⚓ anchor|🏠 house;home|🏡 house with garden|🏢 office building;work|🏥 hospital|🏦 bank|🏨 hotel|🏫 school|🗽 statue of liberty|🗼 tokyo tower|🏰 castle|⛩️ shinto shrine|🕌 mosque|⛪ church|🏝️ desert island|🏖️ beach with umbrella;holiday vacation|🏔️ snow capped mountain|🌋 volcano|🗺️ world map|🧭 compass|🌍 globe showing europe and africa;earth world|🌎 globe showing americas;earth world|🌏 globe showing asia and australia;earth world|🌇 sunset;city|🌃 night with stars;city|🌉 bridge at night"),
        ("Objects", "💡", "⌚ watch|📱 mobile phone;smartphone|💻 laptop;computer|⌨️ keyboard|🖥️ desktop computer|🖨️ printer|💾 floppy disk;save|📷 camera;photo|📸 camera with flash|📹 video camera|📺 television;tv|📻 radio|🔋 battery|🔌 electric plug|💡 light bulb;idea|🔦 flashlight;torch|🕯️ candle|💸 money with wings;spending|💵 dollar banknote;money cash|💳 credit card;payment|🧾 receipt;invoice|💰 money bag;cash|⚖️ balance scale;justice|🔧 wrench;fix tool|🔨 hammer;tool build|⚙️ gear;settings cog|🧲 magnet|🔒 locked;lock secure private|🔓 unlocked|🔑 key|🚪 door|🛏️ bed;sleep|🛁 bathtub|🧹 broom;cleaning|🧼 soap;wash|🔍 magnifying glass;search find zoom|🔭 telescope|🔬 microscope|💊 pill;medicine|🩹 adhesive bandage;plaster|🩺 stethoscope|✉️ envelope;mail letter|📧 e-mail;email|📦 package;box delivery parcel|📜 scroll;document|📄 page facing up;document file|📊 bar chart;stats data|📈 chart increasing;growth up|📉 chart decreasing;loss down|📋 clipboard;paste|📌 pushpin;pin|📎 paperclip;attachment|✂️ scissors;cut|📏 straight ruler;measure|🖊️ pen|📝 memo;note write edit|📚 books;reading study|📖 open book;read|🔖 bookmark;save|🗓️ spiral calendar;date schedule|⏰ alarm clock;wake time|⏳ hourglass not done;waiting|🧳 luggage;travel suitcase|👑 crown;king queen|💍 ring;engagement wedding|💎 gem stone;diamond|👓 glasses|🕶️ sunglasses|🎓 graduation cap;school degree|👕 t-shirt;clothes|👟 running shoe;sneaker trainers|☂️ umbrella"),
        ("Symbols", "❤️", "❤️ red heart;love|🧡 orange heart|💛 yellow heart|💚 green heart|💙 blue heart|💜 purple heart|🖤 black heart|🤍 white heart|🤎 brown heart|💔 broken heart;heartbreak|❣️ heart exclamation|💕 two hearts;love|💞 revolving hearts|💓 beating heart|💗 growing heart|💖 sparkling heart|💘 heart with arrow;cupid|💝 heart with ribbon;gift|💤 zzz;sleep snore|💢 anger symbol|💥 collision;boom explosion|💫 dizzy;stars|💦 sweat droplets;splash|💬 speech balloon;comment message|💭 thought balloon;thinking|♻️ recycling symbol|✅ check mark button;yes done ok tick|❌ cross mark;no wrong delete|⭕ hollow red circle|❗ exclamation mark;important|❓ question mark|‼️ double exclamation mark|⚠️ warning;caution|🚫 prohibited;no ban forbidden|✔️ check mark;tick done|➕ plus|➖ minus|➗ divide|✖️ multiply|🔁 repeat;loop|🔀 shuffle;random|▶️ play button|⏸️ pause button|⏹️ stop button|⏭️ next track button;skip|⬆️ up arrow|⬇️ down arrow|⬅️ left arrow|➡️ right arrow|↩️ right arrow curving left;reply back|🔄 counterclockwise arrows;refresh sync|🔔 bell;notification alert|🔕 bell with slash;mute silent|📢 loudspeaker;announce|📣 megaphone;shout|🔇 muted speaker;mute|🔊 speaker high volume;loud sound|🎵 musical note;music|🎶 musical notes;music|💯 hundred points;perfect 100|🆗 ok button|🆕 new button|♾️ infinity")
    ];

    private static Emoji[]? all;
    private static Dictionary<string, Emoji[]>? groups;

    /// <summary>Group names in tab order, each with the glyph that stands for it in the tab strip.</summary>
    public static IReadOnlyList<(string Name, string Tab)> Tabs { get; } = [.. Groups.Select(group => (group.Name, group.Tab))];

    public static IReadOnlyList<Emoji> All => all ??= [.. Groups.SelectMany(group => Parse(group.Packed))];

    public static IReadOnlyList<Emoji> Group(string name) =>
        (groups ??= Groups.ToDictionary(group => group.Name, group => Parse(group.Packed))).TryGetValue(name, out var found) ? found : [];

    /// <summary>Every term must match the start of some word, so "cry fa" finds the crying faces.</summary>
    public static IReadOnlyList<Emoji> Search(string? query)
    {
        var terms = (query ?? "").ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return terms.Length == 0 ? [] : [.. All.Where(emoji => terms.All(term => emoji.Glyph == term || emoji.Terms.Contains(' ' + term, StringComparison.Ordinal)))];
    }

    /// <summary>Names a glyph restored from this device's history, which carries no label of its own.</summary>
    public static Emoji Describe(string glyph) =>
        All.FirstOrDefault(emoji => emoji.Glyph == glyph, new Emoji(glyph, "Emoji", ""));

    /// <summary>
    /// Puts an emoji where the caret is, replacing any selection, and reports where the caret
    /// lands afterwards. Offsets are UTF-16 code units - what a textarea's selection reports and
    /// what a .NET string is indexed by - so an emoji already in the draft cannot shift them.
    /// </summary>
    public static (string Text, int Caret) Insert(string? draft, int start, int end, string? emoji)
    {
        draft ??= ""; emoji ??= "";
        var from = Math.Clamp(Math.Min(start, end), 0, draft.Length);
        var to = Math.Clamp(Math.Max(start, end), from, draft.Length);
        return (string.Concat(draft.AsSpan(0, from), emoji, draft.AsSpan(to)), from + emoji.Length);
    }

    private static Emoji[] Parse(string packed) => [.. packed.Split('|').Select(static entry =>
    {
        var space = entry.IndexOf(' ');
        var glyph = entry[..space];
        var rest = entry[(space + 1)..];
        var semicolon = rest.IndexOf(';');
        var name = semicolon < 0 ? rest : rest[..semicolon];
        return new Emoji(glyph, name, ' ' + rest.Replace(";", " ", StringComparison.Ordinal).ToLowerInvariant());
    })];
}

/// <summary>
/// The composer's emoji-keyboard state. It lives outside the component so the one thing that
/// must never happen - the draft changing because the picker opened or closed - is provable:
/// only <see cref="Insert"/> touches text, and toggling has no text to touch.
/// </summary>
public sealed class EmojiKeyboardState
{
    /// <summary>True while the picker occupies the on-screen keyboard's place.</summary>
    public bool Open { get; private set; }
    /// <summary>Where the caret must be put back after Blazor rewrites the textarea's value.</summary>
    public int? PendingCaret { get; private set; }
    public bool Toggle() => Open = !Open;
    public void Close() => Open = false;

    public string Insert(string draft, int start, int end, string emoji)
    {
        var (text, caret) = EmojiCatalog.Insert(draft, start, end, emoji);
        PendingCaret = caret;
        return text;
    }

    public int? TakeCaret() { var caret = PendingCaret; PendingCaret = null; return caret; }
}
