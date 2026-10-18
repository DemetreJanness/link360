// Focused static fallback when REA's provider times out. Never runs the target.
// @category LINK360
import ghidra.app.script.GhidraScript;
import ghidra.app.cmd.disassemble.DisassembleCommand;
import ghidra.app.decompiler.DecompInterface;
import ghidra.app.decompiler.DecompileResults;
import ghidra.program.model.address.Address;
import ghidra.program.model.address.AddressSet;
import ghidra.program.model.listing.Function;
import ghidra.program.model.symbol.SourceType;
import java.nio.file.Files;
import java.nio.file.Path;
public class NativeFunctionInspect extends GhidraScript {
 public void run() throws Exception {
  String[] a=getScriptArgs();Address start=toAddr(a[0]);int size=Integer.parseInt(a[1]);if(size<=0||size>16384)throw new IllegalArgumentException("Function bound outside limit");
  AddressSet body=new AddressSet(start,start.add(size-1));new DisassembleCommand(start,body,true).applyTo(currentProgram,monitor);
  Function function=getFunctionAt(start);if(function==null)function=currentProgram.getFunctionManager().createFunction("candidate_tracking_function",start,body,SourceType.USER_DEFINED);
  function.setBody(body);
  DecompInterface decompiler=new DecompInterface();try{decompiler.openProgram(currentProgram);DecompileResults result=decompiler.decompileFunction(function,40,monitor);
   String output="Static candidate at "+start+"; disassembly seed bytes="+size+"; function body="+function.getBody()+"; no automatic program analysis or runtime execution. Pseudocode may follow shared flow beyond the seed.\n";
   output+=result.decompileCompleted()?result.getDecompiledFunction().getC():"Decompile unavailable: "+result.getErrorMessage();Files.writeString(Path.of(a[2]),output);println("LINK360 focused static output saved");
  }finally{decompiler.dispose();}
 }
}
