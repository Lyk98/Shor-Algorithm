﻿using Microsoft.Quantum.Simulation.Core;
using Microsoft.Quantum.Simulation.Simulators;
using System.Collections.Generic;
using System.Threading;
using System;

namespace Quantum.QShor
{
    class Driver
    {
        const double eps = 0;

        static long gcd(long x, long y)
        {
            if (x == 0) return y;
            if (y == 0) return x;
            return gcd(y, x % y);
        }

        static long qpow(long x, long y, long p)
        {
            if (y == 0) return 1;
            if (y == 1) return x % p;
            long t = qpow(x, y >> 1, p);
            if ((y & 1) == 1) return t * t % p * x % p;
            else return t * t % p;
        }

        static long findOrder(long x, long y)
        {
            long s = 1;
            for(long i=1;;i++)
            {
                s = s * x % y;
                if (s == 1) return i;
            }
        }

        static bool isSquare(long x)
        {
            long s = (long)Math.Sqrt(x);
            return s * s == x;
        }

        static double QfindOrder(long x, long y, int th){
            double SdivR = 0;
            double t = 1.0;
            int tbit = 7;
            using (var sim = new QuantumSimulator())
            {
                var res = OrderFinding.Run(sim, x, y).Result;
                for(int i = 0; i < tbit; i++){
                    t = t * 2.0;
                    if(res[i] == 1){
                        SdivR = SdivR * 2 + 1;
                    }else{
                        SdivR = SdivR * 2;
                    }
                }
                SdivR = SdivR / t;
                //Show the result Qubits
                Console.Write($"Thread {th} --- Result Qubits: ");
                for (int i = 0; i < tbit; i++)
                    Console.Write($"{res[i]}");
                Console.WriteLine($"");
            }       
            return SdivR;   
        }

        static long Qfactorize(long N)
        {
            Random ran = new Random();

            while (true)
            {
                long a = ran.Next((int)(N - 3)) + 2;
                long g = gcd(a, N);
                if (g > 1) continue;
                long r = findOrder(a, N);
                //long r = QfindOrder(a, N);
                if ((r & 1) == 1) continue;
                long y = (qpow(a, r / 2, N) + 1) % N;
                if (y == 0) continue;
                return gcd(y, N);
            }
        }

        static bool coPrime(long x, long y){
            long M = (x > y)? y : x;
            for(int i = 2; i <= M; i++){
                if(x%i == 0 && y%i == 0){
                    return false;
                } 
            }
            return true;
        }

        struct param
        {
            public int thread;
            public long N;
            public param(int thread, long N)
            {
                this.thread = thread;
                this.N = N;
            }
        };

        static Thread[] threadList = new Thread[11];

        static void threadStart(object obj1)
        {
            param para = (param)obj1;
            long N = para.N;
            int th = para.thread;
            Random random = new Random();
            while (true)
            {
                long x = random.Next((int)(N - 1)) + 1;
                if (!coPrime(x, N)) continue;
                System.Console.WriteLine($"Thread {th} --- Trying x = {x},N = {N}");
                double p = QfindOrder(x, N, th);
                if (Math.Abs(p) < 1e-9) continue;
                Console.WriteLine($"Thread {th} --- The result of order finding is: {p.ToString()}");

                //Console.WriteLine($"{N} = {p} * {N / p}");

                long ans = -1;
                long[] CFE = new long[102], P = new long[102], Q = new long[102];
                CFE[0] = 0;
                Console.Write($"Thread {th} --- The result of continued fraction expansion is: ");
                for (int i = 1; i <= 100; i++)
                {
                    CFE[i] = (long)Math.Floor(1.0 / p + eps);
                    Console.Write($"{CFE[i]} ");
                    p = 1.0 / p + eps - CFE[i];
                    P[i + 1] = 0;
                    Q[i + 1] = 1;
                    for (int j = i; j >= 1; j--)
                    {
                        P[j] = Q[j + 1];
                        Q[j] = P[j + 1] + Q[j + 1] * CFE[j];
                        long g = gcd(P[j], Q[j]);
                        P[j] /= g;
                        Q[j] /= g;
                        // P[1], Q[1]: similar fraction, CFE[i]: continued fraction expansion
                        if (Q[1] != 0 && (Q[1] & 1) == 0 && qpow(x, Q[1], N) == 1)
                        {
                            ans = Q[1];
                            break;
                        }
                    }
                    if (ans != -1) break;
                    if (Math.Abs(p) < 1e-9) break;
                }
                Console.WriteLine("");
                if (ans == -1) continue;
                Console.WriteLine($"Thread {th} --- Found {x}^{ans} MOD {N} = 1, ans = {ans}");
                long p1 = (qpow(x, ans / 2, N) - 1 + N) % N, p2 = (qpow(x, ans / 2, N) + 1 + N) % N;
                System.Console.WriteLine($"p1 = ({x}^({ans} / 2) - 1) MOD {N} = {p1}, p2 = ({x}^({ans} / 2) + 1) MOD {N} = {p2}");
                if (p1 == 0 || p2 == 0) continue;
                p1 = gcd(p1, N);
                p2 = gcd(p2, N);
                if (p1 > 1) System.Console.WriteLine($"Thread {th} --- Result: {N} = {p1} * {N / p1}");
                else System.Console.WriteLine($"Thread {th} --- Result: {N} = {p2} * {N / p2}");

                Console.WriteLine($"Press any key to exit ...");
                for (int i = 1; i <= 10; i++)
                    if (i != th)
                        threadList[i].Abort();
                Console.ReadKey();
                System.Environment.Exit(0);
            }
        }

        static void Main(string[] args)
		{
            Console.WriteLine($"Please input the product of two prime numbers ...");
            long N = Convert.ToInt64(Console.ReadLine());
            Console.WriteLine($"Fatorizing using Shor Quantum Algorithm ...");
            
            for (int i = 1; i <= 10; i++)
            {
                threadList[i] = new Thread(new ParameterizedThreadStart(threadStart));
                threadList[i].Start(new param(i, N));
            }
		}
	}
}