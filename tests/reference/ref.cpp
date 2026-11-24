#define __forceinline inline
#include <cmath>
#include <cstdio>
#include <cstddef>
#include "accel-classic.hpp"
#include "accel-natural.hpp"
#include "accel-lookup.hpp"
using namespace rawaccel;
int main(){
  double xs[]={0.5,2,5,10,20,40,80};
  // classic gain cap out
  for(int g=0;g<2;g++) for(int cm=0;cm<3;cm++){
    accel_args a; a.mode=accel_mode::classic; a.gain=g; a.input_offset=2; a.acceleration=0.03; a.exponent_classic=2.3;
    a.cap={15,1.6}; a.cap_mode=(rawaccel::cap_mode)cm;
    for(double x:xs){ double v = g? classic<GAIN>(a)(x,a) : classic<LEGACY>(a)(x,a); printf("classic g%d c%d %g %.9f\n",g,cm,x,v);}
  }
  for(int g=0;g<2;g++){
    accel_args a; a.mode=accel_mode::natural; a.gain=g; a.input_offset=1.5; a.decay_rate=0.2; a.limit=1.8;
    for(double x:xs){ double v = g? natural<GAIN>(a)(x,a) : natural<LEGACY>(a)(x,a); printf("natural g%d %g %.9f\n",g,x,v);}
  }
  for(int g=0;g<2;g++){
    accel_args a; a.mode=accel_mode::lookup; a.gain=g; float d[]={1,1, 4,1.1, 16,1.5, 40,2}; a.length=8; for(int i=0;i<8;i++)a.data[i]=d[i];
    lookup l(a);
    for(double x:xs){ printf("lut g%d %g %.9f\n",g,x,l(x,a));}
  }
}
