namespace RapidsLang;

public static class TestPrograms
{
    public static readonly string CommentTest = """
    // Do something awesome
    pipe twitch.chat `<{user}>: {text /*Ive got a comment*/ + ` ok bye ruby  love you{/*comment*/`.`}`}` minecraft.chat; // Pipe the twitch chat format into this.
    """.Trim();

    public static readonly string HelloWorld = """
    use console;
    // should print "Hello, World"
    print(`Hello, World!`);
    """;

    public static readonly string PrintFormatted = """
    use console;
    // should print "Hello, World"
    print(`Hello, {3.5} World!`);
    """;

    public static readonly string HelloFiveTimes = """
    use console;
    
    let i = 0;
    
    while(i < 5) {
        print(`Hello #{i + 1}`);

        i += 1;
    }
    """;
    
    public static readonly string TuringTest = """
    use console;
 
    let i = 0;
    while (true) {
      i += 1;
      if (i % 2 == 0) {
         print(i);
     }
    }
    """;
}