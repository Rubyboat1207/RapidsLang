namespace RapidsLang;

public static class TestPrograms
{
    public static readonly string CommentTest = """
    // Do something awesome
    pipe twitch.chat `<{user}>: {text /*Ive got a comment*/ + ` ok bye ruby  love you{/*comment*/`.`}`}` minecraft.chat; // Pipe the twitch chat format into this.
    """.Trim();
}