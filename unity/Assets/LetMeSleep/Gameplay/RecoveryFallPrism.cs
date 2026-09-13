using System;
using System.Collections.Generic;

namespace LetMeSleep.Gameplay
{
    /// <summary>Immutable authored XZ prism; holes remain dry including their tolerance band.</summary>
    public sealed class RecoveryFallPrism
    {
        private readonly Float2[] outer;
        private readonly Float2[][] holes;
        private readonly float minX,maxX,minZ,maxZ,minY,maxY,tolerance;
        public RecoveryFallPrism(IReadOnlyList<Float2> exterior, IReadOnlyList<Float2[]> exclusions,
            float bottom, float top, float edgeTolerance)
        {
            if (!MathEx.Finite(bottom) || !MathEx.Finite(top) || bottom >= top
                || !MathEx.Finite(edgeTolerance) || edgeTolerance < 0 || edgeTolerance > .02f)
                throw new ArgumentException("Invalid recovery prism heights/tolerance.");
            outer=CopyRing(exterior); minY=bottom; maxY=top; tolerance=edgeTolerance;
            minX=maxX=outer[0].X; minZ=maxZ=outer[0].Y;
            foreach(var p in outer) { minX=Math.Min(minX,p.X);maxX=Math.Max(maxX,p.X);minZ=Math.Min(minZ,p.Y);maxZ=Math.Max(maxZ,p.Y); }
            int count=exclusions?.Count??0; if(count>16) throw new ArgumentException("Too many recovery polygon holes.");
            holes=new Float2[count][]; int vertices=outer.Length;
            for(int i=0;i<count;i++)
            {
                holes[i]=CopyRing(exclusions[i]);vertices+=holes[i].Length;
                if(vertices>8192)throw new ArgumentException("Too many recovery polygon vertices.");
                foreach(var p in holes[i]) if(Classify(outer,p,0)!=1)throw new ArgumentException("Hole must be strictly inside exterior.");
                if(RingsCross(outer,holes[i]))throw new ArgumentException("Hole crosses exterior.");
                for(int j=0;j<i;j++)
                    if(RingsCross(holes[i],holes[j]) || Classify(holes[i],holes[j][0],0)!=0 || Classify(holes[j],holes[i][0],0)!=0)
                        throw new ArgumentException("Polygon holes overlap or nest.");
            }
        }
        public bool Contains(Float3 local)
        {
            if(!local.IsFinite || local.Y<minY || local.Y>maxY || local.X<minX-tolerance
                || local.X>maxX+tolerance || local.Z<minZ-tolerance || local.Z>maxZ+tolerance)return false;
            var p=new Float2(local.X,local.Z);
            if(Classify(outer,p,tolerance)==0)return false;
            foreach(var hole in holes) if(Classify(hole,p,tolerance)!=0)return false;
            return true;
        }
        private static Float2[] CopyRing(IReadOnlyList<Float2> input)
        {
            if(input==null || input.Count<3 || input.Count>2049)throw new ArgumentException("Invalid polygon ring size.");
            int count=input.Count;
            if(Equal(input[0],input[count-1]))count--;
            if(count<3 || count>2048)throw new ArgumentException("Invalid polygon vertex count.");
            var ring=new Float2[count];double area=0;
            for(int i=0;i<count;i++)
            {
                var p=input[i];if(!p.IsFinite || Math.Abs(p.X)>10000 || Math.Abs(p.Y)>10000)throw new ArgumentException("Invalid polygon vertex.");
                ring[i]=p;
            }
            for(int i=0;i<count;i++)
            {
                var a=ring[i];var b=ring[(i+1)%count];
                if(Equal(a,b))throw new ArgumentException("Duplicate adjacent polygon vertex.");
                area+=(double)a.X*b.Y-(double)b.X*a.Y;
                for(int j=i+1;j<count;j++)
                {
                    if(j==i+1 || (i==0 && j==count-1))continue;
                    if(Crosses(a,b,ring[j],ring[(j+1)%count]))throw new ArgumentException("Self-intersecting polygon.");
                }
            }
            if(Math.Abs(area)<1e-8)throw new ArgumentException("Degenerate polygon.");
            return ring;
        }
        // 0 outside, 1 inside, 2 edge/tolerance band. Winding independent.
        private static int Classify(Float2[] ring,Float2 p,float epsilon)
        {
            bool inside=false;
            for(int i=0,j=ring.Length-1;i<ring.Length;j=i++)
            {
                var a=ring[j];var b=ring[i];
                double dx=(double)b.X-a.X,dz=(double)b.Y-a.Y;
                double t=Math.Max(0,Math.Min(1,(((double)p.X-a.X)*dx+((double)p.Y-a.Y)*dz)/(dx*dx+dz*dz)));
                double px=p.X-(a.X+t*dx),pz=p.Y-(a.Y+t*dz);
                if(px*px+pz*pz<=(double)epsilon*epsilon+1e-16)return 2;
                if((a.Y>p.Y)!=(b.Y>p.Y) && p.X<a.X+((double)p.Y-a.Y)*dx/dz)inside=!inside;
            }
            return inside?1:0;
        }
        private static bool RingsCross(Float2[] a,Float2[] b)
        {
            for(int i=0;i<a.Length;i++)for(int j=0;j<b.Length;j++)
                if(Crosses(a[i],a[(i+1)%a.Length],b[j],b[(j+1)%b.Length]))return true;
            return false;
        }
        private static double Side(Float2 a,Float2 b,Float2 c)=>((double)b.X-a.X)*((double)c.Y-a.Y)-((double)b.Y-a.Y)*((double)c.X-a.X);
        private static bool OnSegment(Float2 a,Float2 b,Float2 p)=>Math.Abs(Side(a,b,p))<1e-9
            && p.X>=Math.Min(a.X,b.X) && p.X<=Math.Max(a.X,b.X) && p.Y>=Math.Min(a.Y,b.Y) && p.Y<=Math.Max(a.Y,b.Y);
        private static bool Crosses(Float2 a,Float2 b,Float2 c,Float2 d)
        {
            double abC=Side(a,b,c),abD=Side(a,b,d),cdA=Side(c,d,a),cdB=Side(c,d,b);
            return (abC*abD<0 && cdA*cdB<0) || OnSegment(a,b,c) || OnSegment(a,b,d) || OnSegment(c,d,a) || OnSegment(c,d,b);
        }
        private static bool Equal(Float2 a,Float2 b)=>a.X==b.X && a.Y==b.Y;
    }
}
