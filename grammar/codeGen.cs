using System.ComponentModel.Design;
using System.Reflection;
using System.Threading.Tasks.Dataflow;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;

using SPADE.Grammar.descriptor;
using SPADE.Grammar.Enums;

namespace SPADE.Grammar;

class codeGen : IcollListener {
    public Dictionary<string, UtilCollection> map;

    public Stack<FormatDescriptor> formatStack;
    public SetDescriptor ?setDescriptor;

    public Stack<SetDescriptor> setDescriptorStack;

    public Stack<ConstraintDescriptor> constraintDescriptorStack;
    public Stack<Stack<ConstraintDescriptor>> constraintDescriptorStackStack;

    public codeGen() : base() 
    {
        map = new();
        formatStack = new();
        setDescriptorStack = new();
        constraintDescriptorStack = new();
        constraintDescriptorStackStack = new();
    }

    public void Parse(UtilCollection utilCollection)
    {
        setDescriptor!.isMember(utilCollection);
    }

    /// <summary>
    /// Enter a parse tree produced by <see cref="collParser.start"/>.
    /// </summary>
    /// <param name="context">The parse tree.</param>
    public void EnterStart([NotNull] collParser.StartContext context)
    {

    }
    /// <summary>
    /// Exit a parse tree produced by <see cref="collParser.start"/>.
    /// </summary>
    /// <param name="context">The parse tree.</param>
    public void ExitStart([NotNull] collParser.StartContext context)
    {
        setDescriptor = setDescriptorStack.Pop();

        if (setDescriptorStack.Count() != 0)
            throw new Exception("set Descriptor stack not empty");

    }
    /// <summary>
    /// Enter a parse tree produced by <see cref="collParser.format"/>.
    /// </summary>
    /// <param name="context">The parse tree.</param>
    public void EnterFormat([NotNull] collParser.FormatContext context)
    {
        /*
        if (context.format().Length != 0)
        {
            UtilCollection curr = formatStack.Pop();
            for (int i = context.format().Length - 1; i >= 0; i--) 
            //has to iterate backwards, since the leftmost one should be popped first
            {
                formatStack.Push(curr[i]);
            }
        }
        if (context.TERM() != null)
        {
            map.Add(context.TERM().ToString()!, formatStack.Pop());
        }
    */
    }
    /// <summary>
    /// Exit a parse tree produced by <see cref="collParser.format"/>.
    /// </summary>
    /// <param name="context">The parse tree.</param>
    public void ExitFormat([NotNull] collParser.FormatContext context)
    {
        if (context.format().Length != 0)
        {
            List<FormatDescriptor> lst = new();
            for (int i = 0; i < context.format().Length; i++)
            {
                lst.Add(formatStack.Pop());           
            }
            lst.Reverse(); // have to reverse the list, since the leftmost should be the one that was popped first
            formatStack.Push(new FormatDescriptor(lst));
        }
        if (context.TERM() != null)
        {
            formatStack.Push(new FormatDescriptor(context.TERM().ToString()!));
        }

        /*
        if (context.Parent is not collParser.FormatContext)
        {
            map = new();
            FormatDescriptor form = formatStack.Pop();
            if (formatStack.Count() != 0) throw new Exception("formatStack not empty after eval");
            form.parseFunc(map, collection);
        }
        */
    }
    /// <summary>
    /// Enter a parse tree produced by <see cref="collParser.constraint"/>.
    /// </summary>
    /// <param name="context">The parse tree.</param>
    public void EnterConstraint([NotNull] collParser.ConstraintContext context)
    {
    }
    /// <summary>
    /// Exit a parse tree produced by <see cref="collParser.constraint"/>.
    /// </summary>
    /// <param name="context">The parse tree.</param>
    public void ExitConstraint([NotNull] collParser.ConstraintContext context)
    {
        if (context.@object().BUILT_IN() != null)
        {
            constraintDescriptorStack.Push(new ConstraintDescriptor(context.TERM().ToString()!, context.DEFINITION().ToString()!, context.@object().BUILT_IN().ToString()!));
        }
        else if (context.@object().BUILT_IN_SET() != null && context.DEFINITION().ToString() == "is")
        {
            // "N is int" is a type check, like "N is set". The integer set descriptor
            // pushed for it is only needed by other definitions, such as "S subset int".
            setDescriptorStack.Pop();
            constraintDescriptorStack.Push(new ConstraintDescriptor(context.TERM().ToString()!, context.DEFINITION().ToString()!, context.@object().BUILT_IN_SET().ToString()!));
        }
        else
        {
            constraintDescriptorStack.Push(new ConstraintDescriptor(context.TERM().ToString()!, context.DEFINITION().ToString()!, setDescriptorStack.Pop()));
        }
    }
    /// <summary>
    /// Enter a parse tree produced by <see cref="collParser.object"/>.
    /// </summary>
    /// <param name="context">The parse tree.</param>
    public void EnterObject([NotNull] collParser.ObjectContext context)
    {
    }
    /// <summary>
    /// Exit a parse tree produced by <see cref="collParser.object"/>.
    /// </summary>
    /// <param name="context">The parse tree.</param>
    public void ExitObject([NotNull] collParser.ObjectContext context)
    {
        if (context.BUILT_IN() != null)
        {
            //setDescriptorStack.Push(new SetDescriptor(map, context.BUILT_IN().ToString()!));
        }

        if (context.BUILT_IN_SET() != null)
        {
            setDescriptorStack.Push(new SetDescriptor(map, BuiltInSet.integers));
        }

        if (context.TERM() != null)
        {
            setDescriptorStack.Push(new SetDescriptor(map, context.TERM().ToString()!));
        }

        if (context.BIN_OP() != null)
        {
            SetDescriptor right = setDescriptorStack.Pop();
            SetDescriptor left = setDescriptorStack.Pop();
            switch (context.BIN_OP().ToString())
            {
                case "union":
                    setDescriptorStack.Push(left.Union(right));
                    break;
                case "intersection":
                    setDescriptorStack.Push(left.Intersect(right));
                    break;
                case "cross":
                    setDescriptorStack.Push(left.Cross(right));
                    break;
                case "unorderedcross":
                    setDescriptorStack.Push(left.UnorderedCross(right));
                    break;
            }

        }
    }
    /// <summary>
    /// Enter a parse tree produced by <see cref="collParser.set"/>.
    /// </summary>
    /// <param name="context">The parse tree.</param>
    public void EnterSet([NotNull] collParser.SetContext context)
    {
        constraintDescriptorStackStack.Push(constraintDescriptorStack);
        constraintDescriptorStack = new();
    }
    /// <summary>
    /// Exit a parse tree produced by <see cref="collParser.set"/>.
    /// </summary>
    /// <param name="context">The parse tree.</param>
    public void ExitSet([NotNull] collParser.SetContext context)
    {
        setDescriptorStack.Push(
            new SetDescriptor(map, formatStack.Pop(), new List<ConstraintDescriptor>(constraintDescriptorStack))
        );

        constraintDescriptorStack = constraintDescriptorStackStack.Pop();

    }
    /// <summary>
    /// Enter a parse tree produced by <see cref="collParser.list"/>.
    /// </summary>
    /// <param name="context">The parse tree.</param>
    public void EnterList([NotNull] collParser.ListContext context)
    {
    }
    /// <summary>
    /// Exit a parse tree produced by <see cref="collParser.list"/>.
    /// </summary>
    /// <param name="context">The parse tree.</param>
    public void ExitList([NotNull] collParser.ListContext context)
    {
    }

    public void VisitTerminal(ITerminalNode node)
    {
    }

    public void VisitErrorNode(IErrorNode node)
    {
    }

    public void EnterEveryRule(ParserRuleContext ctx)
    {
    }

    public void ExitEveryRule(ParserRuleContext ctx)
    {
    }
}