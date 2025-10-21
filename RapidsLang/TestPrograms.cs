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
      if(i > 20000) {
         print(`leaving loop.`);
         break;
      }
    }
    """;

    public static readonly string ListTest = """
    use console;
    
    let array = [];
    
    array.add(`Hello, World!`);
    
    print(array[0]);
    
    array.add(`Test123`);
    
    print(array[1]);
    
    let i = 0;
    while(i < 10) {
    array.add(i);
    
    i += 1;
    }
    
    while(i >= 0) {
    print(array[i]);
    
    i -= 1;
    }
    
    """;

    public static readonly string FunctionTest = """
    use console;
    
    do()> {
        print(`Hello, Functions`);
    }
    
    do();
    """;

    public static readonly string FunctionExpressionTest = """
    use console;
    
    get_number()> {
       return 5;
    }
    
    print(get_number());
    """;

    public static readonly string AddFunctionTest = """
    use console;
    
    add(a, b)> {
       return a + b;
    }

    print(add(0.5, 1));
    """;

    public static readonly string ArrayModuleTest = """
    use console;
    use arrays;
    
    // returns array with 16 ones.
    let arr = filledArray(1, 16);
    
    let i = 0;
    while(i < arr.length) {
       print(`{i + 1}: {arr[i]}`);
       
       i += 1;
    }
    """;

    public static readonly string ArrayAssignmentTest = """
    use console;
    use arrays;
  
    let arr = filledArray(1, 16);
  
    arr[0] = 15;
  
    let i = 0;
    while(i < arr.length) {
      arr[i] += 1;
      print(arr[i]);
      i += 1;
    }
  
    """;

    public static readonly string ObjectTest = """
    use console;
    
    const obj = {
        test: `Hello, Objects`
    };

    obj.testAgain = `Hello, Assignments`;
    
    print(obj.test);
    print(obj.testAgain);
    
    obj.test = `Hello, Reassignments`;
    print(obj.test);
    """;

    public static readonly string RemoveArrayElements = """
    use console;
    
    const arr = [1,2,3,4,5,6,7,8,9,10];
    
    arr.removeAt(0);
    arr.removeAt(arr.length - 1);
    
    print(arr);
    
    """;

    public static readonly string RedefineWhileCheck = """
    use console;
    
    let arr = [5, 10, 15, 20];
    let i = 0;
    
    while(i < arr.length) {
        let element = arr[i];
        
        print(element);
        
        i += 1;
    }
    """;

    public static readonly string IfElseTest = """"
    use console;
    
    let str = input(`What's your name?`);
    
    if(str == `ruby`) {
        print(`Hello... you.`);
    }else if(str == `your mom`) {
        print(`thats not nice...`);
    }
    
    """";

    public static readonly string ReturnFromIfStatementTest = """
    use console;
    
    print(()> {
        if(true) {
            return 2;
        }
    }());
    """;
    
    public static readonly string BrainFuckInterpreter = """
    use console: putChar;
    use strings: charFromCode;
    use arrays;
    
    // Hello, World!
    let program = `>>+<--[[<++>->-->+++>+<<<]-->++++]<<.<<-.<<..+++.>.<<-.>.+++.------.>>-.<+.>>.`;
    let ptr = 0;
    
    // build jumptable
    buildJumpTable()> {
        let table = {};
        
        let loopStack = [];
        
        let i = 0;
        while(i < program.length) {
            if(program[i] == `[`) {
                loopStack.insert(0, i);
            }
            if(program[i] == `]`) {
                table[i] = loopStack.pop();
            }
            
            i += 1;
        }
        
        return table;
    }
    
    const jumptable = buildJumpTable();
    
    const tape = filledArray(0, 3000);
    let tapePtr = 0;
    
    let programPointer = 0;
    
    while(programPointer < program.length) {
        let command = program[programPointer];
    
        if(command == `+`) {
            tape[tapePtr] += 1;
            if(tape[tapePtr] > 255) {
                tape[tapePtr] = 0;
            }
        }
        else if(command == `-`) {
            tape[tapePtr] -= 1;
            if(tape[tapePtr] < 0) {
                tape[tapePtr] = 255;
            }
        }
        else if(command == `>`) {
            tapePtr += 1;
        }
        else if(command == `<`) {
            tapePtr -= 1;
        }
        else if(command == `.`) {
            putChar(charFromCode(tape[tapePtr]));
        }
        else if(command == `]`) {
            if(tape[tapePtr] > 0) {
                programPointer = jumptable[programPointer] - 1;
            }
        }
    
        programPointer += 1;
    }
    
    
    """;
}