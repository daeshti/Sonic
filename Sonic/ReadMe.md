# Credits
This project would have not been possible without help of:
- [faf](https://github.com/errantmind/faf)
- [Anders Hejlsberg](https://www.linkedin.com/in/ahejlsberg) for proving that even gods make mistakes such as making functions colored in C#.
## Jargons
This is a system project at heart, so we have to choose between either shortening our words, or shortening our lifespan.

### 1 letter abbreviations
  - B: Byte
  - E: Event
  - I: Integer
  - N: Native
  - U: Unsigned Integer
  
### 2 letter abbreviations 
  - Ep: Epoll
  - Fd: File Descriptor
  - Id: Identifier  

### 3 letter abbreviations
  - Clk: Cloak
  - Cnt: Count
  - Eol: End Of Line
  - Idx: Index
  - Ins: Instance
  - Len: Length, Size
  - Num: Number
  - Opt: Option
  - Pos: Position
  - Ptr: Pointer
  - Rem: Remaining
  - Res: Response, Result
  - Ret: Return
  - Req: Request
  - Sec: Second
  - Str: String
  - Sys: System
  - Thr: Thread  

### 4 letter abbreviations
  - Addr: Address
  - Buff: Buffer
  - Conf: Configuration
  - Conn: Connection
  - Ctrl: Control
  - Curr: Current
  - Patt: Pattern
  - Prog: Program
  - Sock: Socket

### 5 letter abbreviations
  - Const: Constant

## Unsafe Blocks
There are parts of the code that we have to use unsafe blocks. These scopes must be as short as could be and only contain unsafe operations. You might suspect adding 3 lines for a single operation would clutter the code and make it ugly, but I am sure if I shoot myself in the foot it would look even uglier. The only other accepted alternative is to use a tall unsafe block and add an ASCI dragon art comment one line before each operation.

```C#
                \||/
                |  @___oo
      /\  /\   / (__,,,,|
     ) /^\) ^\/ _)
     )   /^\/   _)
     )   _ /  / _)
 /\  )/\/ ||  | )_)
<  >      |(,,) )__)
 ||      /    \)___)\
 | \____(      )___) )___
  \______(_______;;; __;;;
```