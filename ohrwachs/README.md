# Ohrwachs

A port of https://github.com/haxko/NE3-Scope to C#

Haxko did all the heavy lifting: protocol analysis. I only made a simple C# WPF app.

![this is it](https://github.com/holgerlembke/NE3-Scope/blob/main/ohrwachs/images/ohrwachs.jpg?raw=true)

![this is it](https://github.com/holgerlembke/NE3-Scope/blob/main/ohrwachs/images/ohrwachs2.jpg?raw=true)

![this is it](https://github.com/holgerlembke/NE3-Scope/blob/main/ohrwachs/images/ohrwachs3.jpg?raw=true)

https://github.com/holgerlembke/NE3-Scope/tree/main/ohrwachs/release has a executable.

## Remarks

### Wifi-Stuff

Totally experimental. I never did that before and found no good *complete* example. This solution seems to
work, but I'm sure I miss a lot of potential pitfalls.

### Ohrwachs-Protocol

My plan was a quick port of https://github.com/haxko/NE3-Scope Couldn't be that difficult, hey? Alas I know nothing
about Python what can be so difficult... 

The Python solution runs not very smooth on my system. Quite a lot of retries. Not satisfying.

So after my port closely following the Python solution I started to optimize it.

First thing: the timeouts. Python solution uses three timer monitors and some if-else-whatever 
blocks. It is quite intransparent. I tend to solve such things with state engines, much more clearer they 
are. During that 
rewrite I noticed that I can solve it with one simple retry pattern: if anything goes wrong, restart 
everything. Does the job.

Second thing: the timeouts. I do receive and then build the jpeg, then need to dispatch this to the GUI. That is
because we live in two threads: the WPF GUI and the OhrwachsEmpfaenger. Does that syncing cause timeouts?

Third thing: the timeouts. Python solution does a [receive] -> [lots of string parsing] -> [byte array move 
arounds to buildjpeg] -> [call some windowed jpeg display thingy] -> [receive] ->... I did the same.

Since protocol uses UDP I reflected about what happens with those pakets if there is no [receive] active. Or 
in my case we are not waiting in the [packet = udpReceiver.Receive(ref sender);]. Are they buffered? How many?
Discarded? What does the OS do? I found no trustable answers, only random snipets on variouse forums or stuff 
about Windows Server Solutions.

So to tackle both potential timeout reasons I decided to add a second thread. OhrwachsEmpfaenger focuses on
receiving and timeout handling. Pakets are passed as they are to Ohrwachs2GuiPumpe which does all the byte 
copying, jpeg building and dispatch to the GUI stuff.

Does it help? Yes and no. There are still timeouts every 100 to 200 images. 















