using System;
using System.Collections.Generic;

internal class Region
{
    public readonly List<List<Region>> children = new List<List<Region>>();
    private double factor;

    public Region(int _w, int _h, Func<double> _rud)
    {
        this.factor = Math.Pow(_rud(), 1.5);
        this.children.Clear();

        const int CHUNK_SIZE = 4;
        if (_w == 1 && _h == 1) return;
        int cw = _w/CHUNK_SIZE, ch = _h/CHUNK_SIZE;
        int difW = _w - CHUNK_SIZE*cw, difH = _h - CHUNK_SIZE*ch;

        for (var a = 0; a < CHUNK_SIZE; a++)
        {
            var tch = a < difH ? ch + 1 : ch;
            if (tch > 0)
            {
                var newRegions = new List<Region>();
                this.children.Add(newRegions);
                for (var b = 0; b < CHUNK_SIZE; b++)
                {
                    var tcw = b < difW ? cw + 1 : cw;
                    if (tcw > 0)
                    {
                        newRegions.Add(new Region(tcw, tch, _rud));
                    }
                }
            }
        }

        const double OWN_WEIGHT = 0.75;
        for (var z = 0; z < 1; z++)
        {
            //1 iterations found by experiment.
            var blurredFactors = new double[this.children.Count][];
            for (var i = 0; i < blurredFactors.Length; i++)
            {
                blurredFactors[i] = new double[this.children[0].Count];
            }

            for (var a = 0; a < this.children.Count; a++)
            {
                int mh = a - 1, ph = a + 1;
                if (mh < 0) mh += this.children.Count;
                if (ph == this.children.Count) ph = 0;
                for (var b = 0; b < this.children[0].Count; b++)
                {
                    int mw = b - 1, pw = b + 1;
                    if (mw < 0) mw += this.children[0].Count;
                    if (pw == this.children[0].Count) pw = 0;
                    blurredFactors[a][b] += this.children[a][b].factor*OWN_WEIGHT;
                    blurredFactors[a][b] += this.children[mh][b].factor*(1 - OWN_WEIGHT)/4;
                    blurredFactors[a][b] += this.children[ph][b].factor*(1 - OWN_WEIGHT)/4;
                    blurredFactors[a][b] += this.children[a][mw].factor*(1 - OWN_WEIGHT)/4;
                    blurredFactors[a][b] += this.children[a][pw].factor*(1 - OWN_WEIGHT)/4;
                }
            }

            for (var a = 0; a < this.children.Count; a++)
            {
                for (var b = 0; b < this.children[0].Count; b++)
                {
                    this.children[a][b].factor = blurredFactors[a][b]; //Set factors.
                }
            }
        }
    }

    public List<List<double>> GetFactors()
    {
        if (this.children.Count == 0)
        {
            return new List<List<double>> {new List<double> {this.factor}};
        }

        var childrenFactors = new List<List<List<List<double>>>>();
        for (var i = 0; i < this.children.Count; i++)
        {
            var item = new List<List<List<double>>>(new List<List<double>>[this.children[0].Count]);
            childrenFactors.Add(item);
        }

        for (var a = 0; a < this.children.Count; a++)
        {
            for (var b = 0; b < this.children[0].Count; b++)
            {
                childrenFactors[a][b] = this.children[a][b].GetFactors();
            }
        }

        int width = 0, height = 0;
        for (var a = 0; a < this.children.Count; a++)
        {
            height += childrenFactors[a][0].Count;
        }

        for (var b = 0; b < this.children[0].Count; b++)
        {
            width += childrenFactors[0][b][0].Count;
        }

        var factors = new List<List<double>>();
        for (var i = 0; i < height; i++)
        {
            var doubles = new List<double>(new double[width]);
            factors.Add(doubles);
        }

        int x = 0, y = 0;
        for (var my = 0; my < this.children.Count; my++)
        {
            for (var iy = 0; iy < childrenFactors[my][0].Count; iy++)
            {
                for (var mx = 0; mx < this.children[0].Count; mx++)
                {
                    for (var ix = 0; ix < childrenFactors[0][mx][0].Count; ix++)
                    {
                        factors[y][x] = childrenFactors[my][mx][iy][ix]*this.factor;
                        x++;
                    }
                }
                y++;
                x = 0;
            }
        }
        return factors;
    }
}